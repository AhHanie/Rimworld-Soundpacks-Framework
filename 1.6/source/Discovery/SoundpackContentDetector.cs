using System;
using System.Collections.Generic;
using System.IO;

namespace Soundpacks_Framework.Discovery
{
    public static class SoundpackContentDetector
    {
        public const string ModSoundpacksFolderName = "Soundpacks";

        public static IEnumerable<string> EnumerateCandidatePackDirs(IEnumerable<string> contentRootsDescendingPriority)
        {
            foreach (var contentRoot in contentRootsDescendingPriority)
            {
                string soundpacksRoot;
                string[] packDirs;
                try
                {
                    soundpacksRoot = Path.Combine(contentRoot, ModSoundpacksFolderName);
                    if (!Directory.Exists(soundpacksRoot)) continue;
                    packDirs = Directory.GetDirectories(soundpacksRoot);
                }
                catch (Exception)
                {
                    continue;
                }

                Array.Sort(packDirs, StringComparer.OrdinalIgnoreCase);

                foreach (var packDir in packDirs)
                {
                    bool hasManifest;
                    try
                    {
                        hasManifest = File.Exists(Path.Combine(packDir, Storage.SoundpackPathPolicy.ManifestFileName));
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (hasManifest) yield return packDir;
                }
            }
        }

        public static bool HasDiscoverableSoundpack(IEnumerable<string> contentRootsDescendingPriority)
        {
            try
            {
                foreach (var _ in EnumerateCandidatePackDirs(contentRootsDescendingPriority))
                {
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool HasDiscoverableSoundpack(string modRootDir)
        {
            return HasDiscoverableSoundpack(new[] { modRootDir });
        }
    }
}
