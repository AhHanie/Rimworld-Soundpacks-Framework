using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Soundpacks_Framework.Audio;
using Soundpacks_Framework.Discovery;
using Soundpacks_Framework.Validation;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Soundpacks_Framework.Runtime
{
    public static class SoundpackRuntimeController
    {
        private static readonly VanillaSoundSnapshot Snapshot = new VanillaSoundSnapshot();
        private static List<SoundpackDiagnostic> _lastActivationDiagnostics = new List<SoundpackDiagnostic>();

        public static IReadOnlyList<SoundpackDiagnostic> LastActivationDiagnostics => _lastActivationDiagnostics;
        public static string ActivePackId { get; private set; }
        public static bool IsActive => ActivePackId != null;

        public static void ActivateSelectedPack()
        {
            Logger.Info("ActivateSelectedPack starting. SoundpackSettings.activePackId = '" + (SoundpackSettings.activePackId ?? "") + "'.");

            var diagnostics = new List<SoundpackDiagnostic>();
            string requestedId = SoundpackSettings.activePackId;

            if (string.IsNullOrEmpty(requestedId))
            {
                _lastActivationDiagnostics = diagnostics;
                SoundpackSettings.activationAttempted = true;
                Logger.Info("No soundpack selected; vanilla/mod audio is unmodified.");
                return;
            }

            var descriptors = SoundpackDiscoveryService.DiscoverAll();
            Logger.Info("Discovery found " + descriptors.Count + " pack(s): " +
                        string.Join(", ", descriptors.Select(d => d.Id + "[" + d.Source + ", loadOk=" + d.LoadSucceeded + ", suppressed=" + d.SuppressedByConflict + "]")));

            var descriptor = descriptors.FirstOrDefault(d => d.Id == requestedId && d.IsSelectable);

            if (descriptor == null)
            {
                string msg = "Selected soundpack '" + requestedId + "' could not be found or is invalid; no soundpack is active this session.";
                diagnostics.Add(SoundpackDiagnostic.Error("active-pack-missing", requestedId, "activation", msg,
                    "Reselect a pack in the Soundpack Manager. Your selection is preserved on disk for diagnostics."));
                Logger.Warning(msg);
                SoundpackSettings.lastActivationDiagnostic = msg;
                _lastActivationDiagnostics = diagnostics;
                SoundpackSettings.activationAttempted = true;
                return;
            }

            Logger.Info("Selected pack '" + descriptor.Id + "' resolved with " + descriptor.Manifest.mappings.Count + " mapping(s) in its manifest; directory=" + descriptor.DirectoryPath);

            var lookup = new RimWorldSoundDefLookup();
            var validation = SoundpackValidator.ValidateForActivation(descriptor.Manifest, descriptor.DirectoryPath, lookup);
            diagnostics.AddRange(validation.Diagnostics);

            Logger.Info("Validation: " + validation.Mappings.Count(m => m.Enabled) + "/" + validation.Mappings.Count + " mapping(s) enabled. " +
                        (validation.Diagnostics.Count > 0
                            ? "Diagnostics: " + string.Join(" | ", validation.Diagnostics.Select(d => d.ToString()))
                            : "No validation diagnostics."));

            var keyToClip = new Dictionary<string, AudioClip>();
            var queue = new AudioDecodeQueue();
            int appliedMappingCount = 0;

            foreach (var mappingResult in validation.EnabledMappings)
            {
                var decodeTasks = new List<Task<AudioDecodeResult>>();
                foreach (var resolvedFile in mappingResult.ResolvedFiles)
                {
                    decodeTasks.Add(queue.EnqueueDecodeAsync(resolvedFile.AbsolutePath));
                }

                try
                {
                    Task.WaitAll(decodeTasks.ToArray());
                }
                catch (AggregateException)
                {
                }

                var clips = new List<AudioClip>();
                for (int i = 0; i < decodeTasks.Count; i++)
                {
                    var resolvedFile = mappingResult.ResolvedFiles[i];
                    var task = decodeTasks[i];
                    AudioDecodeResult decodeResult = task.Status == TaskStatus.RanToCompletion ? task.Result : null;

                    if (decodeResult == null || !decodeResult.Success)
                    {
                        diagnostics.Add(SoundpackDiagnostic.Warn("decode-failed", descriptor.Id, resolvedFile.AbsolutePath,
                            decodeResult?.Error ?? (task.Exception?.GetBaseException().Message ?? "Unhandled decode failure"),
                            "Replace this file with a valid MP3/WAV/OGG source."));
                        continue;
                    }

                    float gain = resolvedFile.File.volume;
                    string clipKey = decodeResult.Audio.ContentHashHex + "@" + gain.ToString("R", CultureInfo.InvariantCulture);
                    if (!keyToClip.TryGetValue(clipKey, out AudioClip clip))
                    {
                        string clipName = "Soundpack_" + decodeResult.Audio.ContentHashHex.Substring(0, 12) +
                            (gain != 1f ? "_g" + Mathf.RoundToInt(gain * 100f) : "");
                        clip = RuntimeClipFactory.CreateClip(decodeResult.Audio, clipName, gain);
                        keyToClip[clipKey] = clip;
                    }
                    clips.Add(clip);
                }

                if (clips.Count == 0)
                {
                    diagnostics.Add(SoundpackDiagnostic.Warn("mapping-decode-empty", descriptor.Id, mappingResult.Mapping.soundDef,
                        "No files for this mapping decoded successfully", "This mapping is disabled; original audio is preserved for this target."));
                    continue;
                }

                SubSoundDef subSound = mappingResult.ResolvedSubSound;
                Snapshot.Capture(subSound);
                subSound.grains = new List<AudioGrain> { new AudioGrain_Soundpack(clips) };
                subSound.ResolveReferences();
                appliedMappingCount++;

                bool appliedVolumeRange = false;
                if (mappingResult.Mapping.volumeRange != null)
                {
                    Snapshot.CaptureVolumeRange(subSound);
                    subSound.volumeRange = new FloatRange(mappingResult.Mapping.volumeRange.min, mappingResult.Mapping.volumeRange.max);
                    appliedVolumeRange = true;
                }

                Logger.Info("Applied mapping soundDef='" + mappingResult.Mapping.soundDef + "' subSound='" +
                            mappingResult.Mapping.subSoundName + "'[" + mappingResult.Mapping.subSoundIndex + "] with " +
                            clips.Count + " clip(s)" + (appliedVolumeRange ? " and a volumeRange override" : "") +
                            ". parentDef='" + (subSound.parentDef?.defName ?? "?") +
                            "' onSameObjectAsSoundDefOf=" + ReferenceEquals(subSound, subSound.parentDef?.subSounds.ElementAtOrDefault(mappingResult.Mapping.subSoundIndex)));
            }

            ActivePackId = descriptor.Id;
            _lastActivationDiagnostics = diagnostics;
            SoundpackSettings.lastActivationDiagnostic = (appliedMappingCount == 0 && descriptor.Manifest.mappings.Count > 0)
                ? "No mappings could be applied for '" + descriptor.Id + "'; see diagnostics."
                : "";
            SoundpackSettings.activationAttempted = true;

            Logger.Info("Activated soundpack '" + descriptor.Id + "': " + appliedMappingCount + " mapping(s) applied, " +
                        keyToClip.Count + " unique clip variant(s) created, " + RuntimeClipFactory.OwnedCount + " total framework-owned clips.");
        }

        public static void DeactivateAndTeardown()
        {
            Snapshot.RestoreAll();
            RuntimeClipFactory.DestroyAllOwned();
            ActivePackId = null;
            _lastActivationDiagnostics = new List<SoundpackDiagnostic>();
        }
    }
}
