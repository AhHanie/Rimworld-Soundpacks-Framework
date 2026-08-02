using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Soundpacks_Framework.Audio;
using Soundpacks_Framework.Models;
using Soundpacks_Framework.Serialization;
using Soundpacks_Framework.Storage;
using Soundpacks_Framework.Validation;

namespace Soundpacks_Framework.Archive
{
    public enum ImportConflictResolution
    {
        Replace,
        Duplicate,
        Cancel
    }

    public sealed class ImportStageResult
    {
        public bool Success;
        public string StagingDir;
        public SoundpackManifest Manifest;
        public bool IdAlreadyInstalled;
        public List<SoundpackDiagnostic> Diagnostics = new List<SoundpackDiagnostic>();

        public static ImportStageResult Fail(List<SoundpackDiagnostic> diagnostics) =>
            new ImportStageResult { Success = false, Diagnostics = diagnostics };
    }

    public static class SoundpackArchiveService
    {
        private static readonly string[] AllowedAudioExtensions = { ".mp3", ".wav", ".ogg" };

        public static ImportStageResult StageImport(string zipPath)
        {
            var diagnostics = new List<SoundpackDiagnostic>();

            if (!File.Exists(zipPath) || !string.Equals(Path.GetExtension(zipPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(SoundpackDiagnostic.Error("not-a-zip", null, zipPath, "Selected file is not a .zip archive", "Choose a .zip soundpack export."));
                return ImportStageResult.Fail(diagnostics);
            }

            List<ZipArchiveEntry> approvedEntries;
            string manifestEntryName;
            using (var stream = File.OpenRead(zipPath))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                if (!PreScan(zip, diagnostics, out approvedEntries, out manifestEntryName))
                {
                    return ImportStageResult.Fail(diagnostics);
                }

                string stagingDir = SoundpackRepository.NewStagingDir();
                try
                {
                    ExtractApprovedEntries(zip, approvedEntries, stagingDir);
                }
                catch (Exception ex)
                {
                    SoundpackRepository.DeleteScratchDirectory(stagingDir);
                    diagnostics.Add(SoundpackDiagnostic.Error("extract-failed", null, zipPath, ex.Message, "Re-export the archive and try again."));
                    return ImportStageResult.Fail(diagnostics);
                }

                return FinishStaging(stagingDir, diagnostics);
            }
        }

        private static bool PreScan(ZipArchive zip, List<SoundpackDiagnostic> diagnostics, out List<ZipArchiveEntry> approvedEntries, out string manifestEntryName)
        {
            approvedEntries = new List<ZipArchiveEntry>();
            manifestEntryName = null;

            if (zip.Entries.Count > SoundpackLimits.MaxArchiveEntries)
            {
                diagnostics.Add(SoundpackDiagnostic.Error("too-many-entries", null, "archive", "Archive has " + zip.Entries.Count + " entries, exceeding the limit of " + SoundpackLimits.MaxArchiveEntries, "Remove unused files from the archive."));
                return false;
            }

            long totalUncompressed = 0;
            long totalCompressed = 0;
            var seenCaseInsensitive = new HashSet<string>();
            int manifestCount = 0;

            foreach (var entry in zip.Entries)
            {
                bool isDirectoryEntry = entry.FullName.EndsWith("/", StringComparison.Ordinal) && entry.Length == 0;
                if (isDirectoryEntry) continue;

                if (!SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath(entry.FullName, out string canonical, out string pathError))
                {
                    diagnostics.Add(SoundpackDiagnostic.Error("unsafe-entry-path", null, entry.FullName, pathError, "Remove or rename this entry and re-export."));
                    return false;
                }

                if (IsLikelySymlink(entry))
                {
                    diagnostics.Add(SoundpackDiagnostic.Error("symlink-entry", null, canonical, "Archive entry appears to be a symlink", "Archives must contain only regular files."));
                    return false;
                }

                bool isManifest = canonical == SoundpackPathPolicy.ManifestFileName;
                bool isAudio = SoundpackPathPolicy.IsUnderAudioFolder(canonical) &&
                               AllowedAudioExtensions.Contains(Path.GetExtension(canonical).ToLowerInvariant());

                if (!isManifest && !isAudio)
                {
                    diagnostics.Add(SoundpackDiagnostic.Error("unexpected-entry", null, canonical,
                        "Entry is not soundpack.json or a supported audio file under Audio/",
                        "Only soundpack.json and Audio/*.mp3|.wav|.ogg are allowed in a soundpack archive."));
                    return false;
                }

                if (isManifest)
                {
                    manifestCount++;
                    manifestEntryName = canonical;
                    if (entry.Length > SoundpackLimits.MaxManifestBytes)
                    {
                        diagnostics.Add(SoundpackDiagnostic.Error("manifest-too-large", null, canonical, "soundpack.json exceeds the " + (SoundpackLimits.MaxManifestBytes / (1024 * 1024)) + " MiB limit", "Reduce manifest size."));
                        return false;
                    }
                }
                else if (entry.Length > SoundpackLimits.MaxPerAudioFileBytes)
                {
                    diagnostics.Add(SoundpackDiagnostic.Error("audio-file-too-large", null, canonical, "File exceeds the " + (SoundpackLimits.MaxPerAudioFileBytes / (1024 * 1024)) + " MiB per-file limit", "Use a smaller source file."));
                    return false;
                }

                if (entry.CompressedLength > 0 && entry.Length / entry.CompressedLength > SoundpackLimits.MaxCompressionRatio)
                {
                    diagnostics.Add(SoundpackDiagnostic.Error("suspicious-compression-ratio", null, canonical, "Entry compression ratio exceeds " + SoundpackLimits.MaxCompressionRatio + ":1", "This looks like a decompression bomb; the archive was rejected."));
                    return false;
                }

                string caseKey = SoundpackPathPolicy.CaseInsensitiveKey(canonical);
                if (!seenCaseInsensitive.Add(caseKey))
                {
                    diagnostics.Add(SoundpackDiagnostic.Error("duplicate-case-insensitive-path", null, canonical, "Archive contains two entries differing only by case", "Rename one of the conflicting entries."));
                    return false;
                }

                totalUncompressed += entry.Length;
                totalCompressed += entry.CompressedLength;
                approvedEntries.Add(entry);
            }

            if (manifestCount != 1)
            {
                diagnostics.Add(SoundpackDiagnostic.Error("manifest-count", null, "archive", "Archive must contain exactly one root soundpack.json, found " + manifestCount, "Ensure soundpack.json is at the archive root."));
                return false;
            }
            if (totalUncompressed > SoundpackLimits.MaxTotalUncompressedBytes)
            {
                diagnostics.Add(SoundpackDiagnostic.Error("total-size-too-large", null, "archive", "Total uncompressed size exceeds " + (SoundpackLimits.MaxTotalUncompressedBytes / (1024 * 1024)) + " MiB", "Remove or compress source audio."));
                return false;
            }
            if (totalCompressed > SoundpackLimits.MaxTotalCompressedBytes)
            {
                diagnostics.Add(SoundpackDiagnostic.Error("compressed-size-too-large", null, "archive", "Total compressed size exceeds " + (SoundpackLimits.MaxTotalCompressedBytes / (1024 * 1024)) + " MiB", "Split the pack or use smaller source audio."));
                return false;
            }

            return true;
        }

        private static bool IsLikelySymlink(ZipArchiveEntry entry)
        {
            uint external = unchecked((uint)entry.ExternalAttributes);
            uint unixMode = external >> 16;
            return (unixMode & 0xF000) == 0xA000;
        }

        private static void ExtractApprovedEntries(ZipArchive zip, List<ZipArchiveEntry> entries, string stagingDir)
        {
            foreach (var entry in entries)
            {
                SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath(entry.FullName, out string canonical, out _);
                string destination = SoundpackPathPolicy.CombineContained(stagingDir, canonical);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));

                using (var entryStream = entry.Open())
                using (var fileStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    entryStream.CopyTo(fileStream);
                }
            }
        }

        private static ImportStageResult FinishStaging(string stagingDir, List<SoundpackDiagnostic> diagnostics)
        {
            string manifestPath = Path.Combine(stagingDir, SoundpackPathPolicy.ManifestFileName);
            string audioDir = Path.Combine(stagingDir, "Audio");

            string manifestText;
            try
            {
                manifestText = File.ReadAllText(manifestPath, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                SoundpackRepository.DeleteScratchDirectory(stagingDir);
                diagnostics.Add(SoundpackDiagnostic.Error("manifest-read-failed", null, manifestPath, ex.Message, "Re-export the archive."));
                return ImportStageResult.Fail(diagnostics);
            }

            var parseResult = SoundpackManifestSerializer.Deserialize(manifestText);
            if (!parseResult.Success)
            {
                SoundpackRepository.DeleteScratchDirectory(stagingDir);
                diagnostics.Add(SoundpackDiagnostic.Error("manifest-invalid", null, manifestPath, parseResult.Error, "Fix soundpack.json and re-export."));
                return ImportStageResult.Fail(diagnostics);
            }

            var manifest = parseResult.Manifest;
            var structureResult = SoundpackValidator.ValidateStructure(manifest);
            diagnostics.AddRange(structureResult.Diagnostics);
            if (!structureResult.StructurallyValid)
            {
                SoundpackRepository.DeleteScratchDirectory(stagingDir);
                return ImportStageResult.Fail(diagnostics);
            }

            foreach (var mapping in manifest.mappings)
            {
                foreach (var file in mapping.files)
                {
                    if (!SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath(file.path, out string canonical, out _)) continue;
                    string absolute = Path.Combine(stagingDir, canonical.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(absolute))
                    {
                        diagnostics.Add(SoundpackDiagnostic.Warn("mapping-file-missing", manifest.id, file.path, "Referenced file was not present in the archive", "Re-export the pack including this file."));
                        continue;
                    }
                    var probe = AudioDecodeService.ProbeHeader(absolute);
                    if (!probe.Success)
                    {
                        diagnostics.Add(SoundpackDiagnostic.Warn("audio-header-invalid", manifest.id, file.path, probe.Error, "Replace this file with a valid MP3/WAV/OGG source."));
                    }
                }
            }

            bool idInstalled = SoundpackPathPolicy.IsValidPackId(manifest.id) &&
                                SoundpackRepository.ListUserPackIds().Any(id => string.Equals(id, manifest.id, StringComparison.Ordinal));

            return new ImportStageResult
            {
                Success = true,
                StagingDir = stagingDir,
                Manifest = manifest,
                IdAlreadyInstalled = idInstalled,
                Diagnostics = diagnostics
            };
        }

        public static bool CommitImport(ImportStageResult staged, ImportConflictResolution resolution, out string finalPackId, out string error)
        {
            finalPackId = null;
            error = null;

            if (!staged.Success)
            {
                error = "Cannot commit a failed staging result.";
                return false;
            }

            if (resolution == ImportConflictResolution.Cancel)
            {
                SoundpackRepository.DeleteScratchDirectory(staged.StagingDir);
                return true;
            }

            string targetId = staged.Manifest.id;
            if (resolution == ImportConflictResolution.Duplicate)
            {
                targetId = SoundpackPathPolicy.NewGuidPackId();
                staged.Manifest.id = targetId;
                string manifestPath = Path.Combine(staged.StagingDir, SoundpackPathPolicy.ManifestFileName);
                File.WriteAllText(manifestPath, SoundpackManifestSerializer.SerializeCanonical(staged.Manifest), new System.Text.UTF8Encoding(false));
            }

            try
            {
                SoundpackRepository.PromoteDirectoryToPack(staged.StagingDir, targetId);
                finalPackId = targetId;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                SoundpackRepository.DeleteScratchDirectory(staged.StagingDir);
                return false;
            }
        }

        public static void CancelImport(ImportStageResult staged)
        {
            if (staged?.StagingDir != null)
            {
                SoundpackRepository.DeleteScratchDirectory(staged.StagingDir);
            }
        }

        public static bool Export(SoundpackManifest manifest, string packRootDir, string destinationZipPath, out List<SoundpackDiagnostic> diagnostics)
        {
            diagnostics = new List<SoundpackDiagnostic>();
            var structureResult = SoundpackValidator.ValidateStructure(manifest);
            diagnostics.AddRange(structureResult.Diagnostics);
            if (!structureResult.StructurallyValid)
            {
                return false;
            }

            var referencedFiles = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var mapping in manifest.mappings)
            {
                foreach (var file in mapping.files)
                {
                    if (SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath(file.path, out string canonical, out _))
                    {
                        referencedFiles.Add(canonical);
                    }
                }
            }

            string directory = Path.GetDirectoryName(destinationZipPath);
            Directory.CreateDirectory(directory);
            string tempPath = Path.Combine(directory, Path.GetFileName(destinationZipPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    var manifestEntry = zip.CreateEntry(SoundpackPathPolicy.ManifestFileName, CompressionLevel.Optimal);
                    SetNormalizedTimestamp(manifestEntry);
                    using (var writer = new StreamWriter(manifestEntry.Open(), new System.Text.UTF8Encoding(false)))
                    {
                        writer.Write(SoundpackManifestSerializer.SerializeCanonical(manifest));
                    }

                    foreach (var canonical in referencedFiles)
                    {
                        string sourcePath = Path.Combine(packRootDir, canonical.Replace('/', Path.DirectorySeparatorChar));
                        if (!File.Exists(sourcePath))
                        {
                            diagnostics.Add(SoundpackDiagnostic.Warn("export-file-missing", manifest.id, canonical, "Referenced file is missing on disk and was skipped", "Re-add the file or remove it from the mapping before exporting."));
                            continue;
                        }
                        var entry = zip.CreateEntry(canonical, CompressionLevel.Optimal);
                        SetNormalizedTimestamp(entry);
                        using (var entryStream = entry.Open())
                        using (var sourceStream = File.OpenRead(sourcePath))
                        {
                            sourceStream.CopyTo(entryStream);
                        }
                    }
                }

                if (File.Exists(destinationZipPath))
                {
                    File.Delete(destinationZipPath);
                }
                File.Move(tempPath, destinationZipPath);
                return true;
            }
            catch (Exception ex)
            {
                AtomicFileWriter.TryDelete(tempPath);
                diagnostics.Add(SoundpackDiagnostic.Error("export-failed", manifest.id, destinationZipPath, ex.Message, "Check disk space and destination permissions, then retry."));
                return false;
            }
        }

        private static void SetNormalizedTimestamp(ZipArchiveEntry entry)
        {
            entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }
    }
}
