using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Soundpacks_Framework.Models;
using Soundpacks_Framework.Storage;
using Verse.Sound;

namespace Soundpacks_Framework.Validation
{
    public sealed class ResolvedSoundpackFile
    {
        public SoundpackAudioFile File;
        public string AbsolutePath;
    }

    public sealed class MappingValidationResult
    {
        public SoundpackMapping Mapping;
        public bool Enabled;
        public SubSoundDef ResolvedSubSound;
        public List<ResolvedSoundpackFile> ResolvedFiles = new List<ResolvedSoundpackFile>();
        public List<SoundpackDiagnostic> Diagnostics = new List<SoundpackDiagnostic>();
    }

    public sealed class ManifestValidationResult
    {
        public bool StructurallyValid = true;
        public List<SoundpackDiagnostic> Diagnostics = new List<SoundpackDiagnostic>();
        public List<MappingValidationResult> Mappings = new List<MappingValidationResult>();

        public bool HasBlockingErrors => !StructurallyValid || Diagnostics.Any(d => d.IsError);
        public IEnumerable<MappingValidationResult> EnabledMappings => Mappings.Where(m => m.Enabled);
    }

    public static class SoundpackValidator
    {
        public static ManifestValidationResult ValidateStructure(SoundpackManifest manifest)
        {
            var result = new ManifestValidationResult();

            if (manifest.schemaVersion != SoundpackManifest.CurrentSchemaVersion)
            {
                result.Diagnostics.Add(SoundpackDiagnostic.Error("schema-version-unsupported", manifest.id,
                    "soundpack.json", "schemaVersion " + manifest.schemaVersion + " is not the supported version " + SoundpackManifest.CurrentSchemaVersion,
                    "Migrate or re-export the pack with a compatible version of Soundpacks Framework."));
            }

            if (string.IsNullOrEmpty(manifest.id) || !SoundpackPathPolicy.IsValidPackId(manifest.id))
            {
                result.Diagnostics.Add(SoundpackDiagnostic.Error("invalid-id", manifest.id,
                    "soundpack.json:id", "Pack id is missing or not a valid lower-case identifier",
                    "Set 'id' to a lower-case reverse-DNS or GUID-like string."));
            }

            if (string.IsNullOrEmpty(manifest.name))
            {
                result.Diagnostics.Add(SoundpackDiagnostic.Error("missing-name", manifest.id,
                    "soundpack.json:name", "Pack name is missing", "Set a non-empty 'name'."));
            }

            var seenTargets = new HashSet<string>();
            for (int i = 0; i < manifest.mappings.Count; i++)
            {
                var mapping = manifest.mappings[i];
                string location = "mappings[" + i + "]";

                if (string.IsNullOrEmpty(mapping.soundDef))
                {
                    result.Diagnostics.Add(SoundpackDiagnostic.Error("mapping-missing-sounddef", manifest.id,
                        location, "Mapping has no soundDef", "Set 'soundDef' to a target def name."));
                }

                if (mapping.files.Count == 0)
                {
                    result.Diagnostics.Add(SoundpackDiagnostic.Error("mapping-no-files", manifest.id,
                        location, "Mapping has no source files; a mapping with no files is never allowed to fall back to silence",
                        "Add at least one file, or remove the mapping."));
                }

                if (mapping.volumeRange != null || mapping.volumeRangeMalformed)
                {
                    bool malformed = mapping.volumeRangeMalformed;
                    bool rangeValid = !malformed &&
                        !float.IsNaN(mapping.volumeRange.min) && !float.IsInfinity(mapping.volumeRange.min) &&
                        !float.IsNaN(mapping.volumeRange.max) && !float.IsInfinity(mapping.volumeRange.max) &&
                        mapping.volumeRange.min >= SoundpackLimits.MinSubSoundVolumeRangePercent &&
                        mapping.volumeRange.max <= SoundpackLimits.MaxSubSoundVolumeRangePercent &&
                        mapping.volumeRange.min <= mapping.volumeRange.max;

                    if (!rangeValid)
                    {
                        string rangeText = malformed
                            ? "malformed"
                            : "min=" + mapping.volumeRange.min.ToString(CultureInfo.InvariantCulture) + " max=" + mapping.volumeRange.max.ToString(CultureInfo.InvariantCulture);
                        result.Diagnostics.Add(SoundpackDiagnostic.Error("invalid-mapping-volume-range", manifest.id,
                            location, "Mapping has volumeRange " + rangeText + ", which is invalid",
                            "Set 'volumeRange.min' and 'volumeRange.max' to numbers between 0 and 100, with min not exceeding max."));
                    }
                }

                string key = mapping.TargetKey();
                if (!seenTargets.Add(key))
                {
                    result.Diagnostics.Add(SoundpackDiagnostic.Error("duplicate-mapping-target", manifest.id,
                        location, "Another mapping already targets soundDef='" + mapping.soundDef + "' subSoundName='" + mapping.subSoundName + "' subSoundIndex=" + mapping.subSoundIndex,
                        "Remove or retarget one of the duplicate mappings."));
                }

                var seenFiles = new HashSet<string>();
                foreach (var file in mapping.files)
                {
                    if (file.volumeMalformed || float.IsNaN(file.volume) || float.IsInfinity(file.volume) ||
                        file.volume < 0f || file.volume > 2f)
                    {
                        string volumeText = file.volumeMalformed ? "a non-numeric value" : file.volume.ToString(CultureInfo.InvariantCulture);
                        result.Diagnostics.Add(SoundpackDiagnostic.Error("invalid-file-volume", manifest.id,
                            location, "File '" + file.path + "' has volume " + volumeText + ", which is outside the valid 0% to 200% range",
                            "Set 'volume' to a number between 0.0 and 2.0 (0% to 200%)."));
                    }

                    string canonical = null;
                    string pathError = null;
                    bool pathOk = false;
                    if (!string.IsNullOrEmpty(file.path) &&
                        SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath(file.path, out canonical, out pathError))
                    {
                        pathOk = SoundpackPathPolicy.IsUnderAudioFolder(canonical);
                    }
                    if (!pathOk)
                    {
                        result.Diagnostics.Add(SoundpackDiagnostic.Error("invalid-file-path", manifest.id,
                            location, "File path '" + file.path + "' is not a valid canonical path under Audio/" + (pathError != null ? " (" + pathError + ")" : ""),
                            "Use a relative path like Audio/example.mp3."));
                        continue;
                    }
                    string caseKey = SoundpackPathPolicy.CaseInsensitiveKey(canonical);
                    if (!seenFiles.Add(caseKey))
                    {
                        result.Diagnostics.Add(SoundpackDiagnostic.Error("duplicate-file-in-mapping", manifest.id,
                            location, "File '" + file.path + "' is listed more than once in this mapping",
                            "Remove the duplicate entry."));
                    }
                }
            }

            result.StructurallyValid = !result.Diagnostics.Any(d => d.IsError);
            return result;
        }

        public static ManifestValidationResult ValidateForActivation(SoundpackManifest manifest, string audioRootDir, ISoundDefLookup lookup)
        {
            var result = ValidateStructure(manifest);

            foreach (var mapping in manifest.mappings)
            {
                var mappingResult = new MappingValidationResult { Mapping = mapping };
                string location = "mapping soundDef='" + mapping.soundDef + "' subSoundName='" + mapping.subSoundName + "' subSoundIndex=" + mapping.subSoundIndex;

                bool blocked = mapping.files.Count == 0 || string.IsNullOrEmpty(mapping.soundDef);

                if (!blocked)
                {
                    if (!lookup.TryResolve(mapping.soundDef, mapping.subSoundName, mapping.subSoundIndex, out var defInfo, out var subInfo, out string resolveError))
                    {
                        mappingResult.Diagnostics.Add(SoundpackDiagnostic.Warn("target-not-found", manifest.id, location,
                            resolveError, "This mapping is disabled; the original grains for this target (if any) are left untouched."));
                        blocked = true;
                    }
                    else
                    {
                        mappingResult.ResolvedSubSound = subInfo.Def;

                        if (!string.IsNullOrEmpty(mapping.expectedSourcePackageId) &&
                            !string.Equals(mapping.expectedSourcePackageId, defInfo.SourcePackageId, System.StringComparison.OrdinalIgnoreCase))
                        {
                            mappingResult.Diagnostics.Add(SoundpackDiagnostic.Warn("source-package-mismatch", manifest.id, location,
                                "Expected source package '" + mapping.expectedSourcePackageId + "' but the winning def now comes from '" + defInfo.SourcePackageId + "'",
                                "Load order changed the effective source of this def; verify the mapping is still correct."));
                        }
                    }
                }

                if (!blocked)
                {
                    foreach (var file in mapping.files)
                    {
                        if (!SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath(file.path, out string canonical, out _))
                        {
                            mappingResult.Diagnostics.Add(SoundpackDiagnostic.Warn("invalid-file-path", manifest.id, location,
                                "File path '" + file.path + "' is invalid", "Fix the file path in the editor."));
                            continue;
                        }
                        string absolute = Path.Combine(audioRootDir, canonical.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(absolute))
                        {
                            mappingResult.Diagnostics.Add(SoundpackDiagnostic.Warn("missing-file", manifest.id, location,
                                "File '" + file.path + "' was not found in the pack", "Re-add the file or remove it from the mapping."));
                            continue;
                        }
                        mappingResult.ResolvedFiles.Add(new ResolvedSoundpackFile { File = file, AbsolutePath = absolute });
                    }

                    if (mappingResult.ResolvedFiles.Count == 0)
                    {
                        mappingResult.Diagnostics.Add(SoundpackDiagnostic.Warn("mapping-no-resolvable-files", manifest.id, location,
                            "None of this mapping's files could be resolved", "This mapping is disabled."));
                        blocked = true;
                    }
                }

                mappingResult.Enabled = !blocked;
                result.Mappings.Add(mappingResult);
                result.Diagnostics.AddRange(mappingResult.Diagnostics);
            }

            return result;
        }
    }
}
