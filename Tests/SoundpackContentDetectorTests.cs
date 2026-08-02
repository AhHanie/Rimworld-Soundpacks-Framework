using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Soundpacks_Framework.Discovery;

namespace Soundpacks_Framework.Tests
{
    public static class SoundpackContentDetectorTests
    {
        public static void Register(TestRunner runner)
        {
            runner.Add("ContentDetector.NoSoundpacksDir.ReturnsFalse", NoSoundpacksDirReturnsFalse);
            runner.Add("ContentDetector.EmptySoundpacksDir.ReturnsFalse", EmptySoundpacksDirReturnsFalse);
            runner.Add("ContentDetector.PackDirWithoutManifest.ReturnsFalse", PackDirWithoutManifestReturnsFalse);
            runner.Add("ContentDetector.PackDirWithManifest.ReturnsTrue", PackDirWithManifestReturnsTrue);
            runner.Add("ContentDetector.MisplacedManifest.ReturnsFalse", MisplacedManifestReturnsFalse);
            runner.Add("ContentDetector.VersionedRootWithNoSoundpacksFolder.StillDetected", VersionedRootWithNoSoundpacksFolderStillDetected);
            runner.Add("ContentDetector.CustomLoadFolder.Detected", CustomLoadFolderDetected);
            runner.Add("ContentDetector.OnlyOneOfSeveralRootsHasPack.Detected", OnlyOneOfSeveralRootsHasPackDetected);
            runner.Add("ContentDetector.MultipleRoots.CandidateOrderMatchesRootPriority", MultipleRootsCandidateOrderMatchesRootPriority);
            runner.Add("ContentDetector.UnreadableRoot.ContinuesToNextRoot", UnreadableRootContinuesToNextRoot);
            runner.Add("ContentDetector.SameIdInTwoRoots.BothYieldedRootFirst", SameIdInTwoRootsBothYieldedRootFirst);
        }

        private static string NewModRoot(string label)
        {
            string dir = Path.Combine(Path.GetTempPath(), "spf_test_contentdetector_" + label + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void NoSoundpacksDirReturnsFalse()
        {
            string modRoot = NewModRoot("nodir");
            try
            {
                Assert.False(SoundpackContentDetector.HasDiscoverableSoundpack(modRoot), "missing Soundpacks directory");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void EmptySoundpacksDirReturnsFalse()
        {
            string modRoot = NewModRoot("empty");
            try
            {
                Directory.CreateDirectory(Path.Combine(modRoot, "Soundpacks"));
                Assert.False(SoundpackContentDetector.HasDiscoverableSoundpack(modRoot), "empty Soundpacks directory");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void PackDirWithoutManifestReturnsFalse()
        {
            string modRoot = NewModRoot("nomanifest");
            try
            {
                Directory.CreateDirectory(Path.Combine(modRoot, "Soundpacks", "my-pack"));
                Assert.False(SoundpackContentDetector.HasDiscoverableSoundpack(modRoot), "pack directory without soundpack.json");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void PackDirWithManifestReturnsTrue()
        {
            string modRoot = NewModRoot("withmanifest");
            try
            {
                string packDir = Path.Combine(modRoot, "Soundpacks", "my-pack");
                Directory.CreateDirectory(packDir);
                File.WriteAllText(Path.Combine(packDir, "soundpack.json"), "not even valid json");
                Assert.True(SoundpackContentDetector.HasDiscoverableSoundpack(modRoot), "pack directory with soundpack.json, regardless of contents");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void MisplacedManifestReturnsFalse()
        {
            string modRoot = NewModRoot("misplaced");
            try
            {
                string soundpacksRoot = Path.Combine(modRoot, "Soundpacks");
                Directory.CreateDirectory(soundpacksRoot);
                File.WriteAllText(Path.Combine(soundpacksRoot, "soundpack.json"), "{}");

                string nestedDir = Path.Combine(soundpacksRoot, "my-pack", "nested");
                Directory.CreateDirectory(nestedDir);
                File.WriteAllText(Path.Combine(nestedDir, "soundpack.json"), "{}");

                Assert.False(SoundpackContentDetector.HasDiscoverableSoundpack(modRoot),
                    "manifest at Soundpacks root or only nested is not a direct-child pack layout");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void WritePack(string contentRoot, string packId)
        {
            string packDir = Path.Combine(contentRoot, "Soundpacks", packId);
            Directory.CreateDirectory(packDir);
            File.WriteAllText(Path.Combine(packDir, "soundpack.json"), "{}");
        }

        private static void VersionedRootWithNoSoundpacksFolderStillDetected()
        {
            string modRoot = NewModRoot("versioned");
            try
            {
                string versionedRoot = Path.Combine(modRoot, "1.6");
                Directory.CreateDirectory(versionedRoot);
                WritePack(versionedRoot, "my-pack");

                var contentRoots = new List<string> { versionedRoot, modRoot };
                Assert.True(SoundpackContentDetector.HasDiscoverableSoundpack(contentRoots),
                    "pack under a versioned content root with no root-level Soundpacks folder");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void CustomLoadFolderDetected()
        {
            string modRoot = NewModRoot("custom");
            try
            {
                string customRoot = Path.Combine(modRoot, "CustomFolder");
                Directory.CreateDirectory(customRoot);
                WritePack(customRoot, "my-pack");

                var contentRoots = new List<string> { customRoot };
                Assert.True(SoundpackContentDetector.HasDiscoverableSoundpack(contentRoots),
                    "pack under a custom LoadFolders.xml-selected root");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void OnlyOneOfSeveralRootsHasPackDetected()
        {
            string modRoot = NewModRoot("onlyone");
            try
            {
                string emptyRoot = Path.Combine(modRoot, "1.6");
                string packRoot = Path.Combine(modRoot, "Common");
                Directory.CreateDirectory(emptyRoot);
                Directory.CreateDirectory(packRoot);
                WritePack(packRoot, "my-pack");

                var contentRoots = new List<string> { emptyRoot, packRoot, modRoot };
                var candidates = SoundpackContentDetector.EnumerateCandidatePackDirs(contentRoots).ToList();

                Assert.Equal(1, candidates.Count, "only the root with a pack should yield a candidate");
                Assert.Equal(Path.Combine(packRoot, "Soundpacks", "my-pack"), candidates[0], "candidate directory should be the pack under the non-empty root");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void MultipleRootsCandidateOrderMatchesRootPriority()
        {
            string modRoot = NewModRoot("order");
            try
            {
                string versionedRoot = Path.Combine(modRoot, "1.6");
                Directory.CreateDirectory(versionedRoot);
                WritePack(versionedRoot, "pack-a");
                WritePack(modRoot, "pack-b");

                var contentRoots = new List<string> { versionedRoot, modRoot };
                var candidates = SoundpackContentDetector.EnumerateCandidatePackDirs(contentRoots).ToList();

                Assert.Equal(2, candidates.Count, "both roots have a pack, so both should be yielded");
                Assert.Equal(Path.Combine(versionedRoot, "Soundpacks", "pack-a"), candidates[0], "higher-priority root's pack should come first");
                Assert.Equal(Path.Combine(modRoot, "Soundpacks", "pack-b"), candidates[1], "lower-priority root's pack should come second");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void UnreadableRootContinuesToNextRoot()
        {
            string modRoot = NewModRoot("unreadable");
            try
            {
                string missingRoot = Path.Combine(modRoot, "DoesNotExist");
                string packRoot = Path.Combine(modRoot, "Common");
                Directory.CreateDirectory(packRoot);
                WritePack(packRoot, "my-pack");

                var contentRoots = new List<string> { missingRoot, packRoot };
                Assert.True(SoundpackContentDetector.HasDiscoverableSoundpack(contentRoots),
                    "a nonexistent earlier root must not stop discovery of a later, valid root");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }

        private static void SameIdInTwoRootsBothYieldedRootFirst()
        {
            string modRoot = NewModRoot("sameid");
            try
            {
                string versionedRoot = Path.Combine(modRoot, "1.6");
                Directory.CreateDirectory(versionedRoot);
                WritePack(versionedRoot, "my-pack");
                WritePack(modRoot, "my-pack");

                var contentRoots = new List<string> { versionedRoot, modRoot };
                var candidates = SoundpackContentDetector.EnumerateCandidatePackDirs(contentRoots).ToList();

                Assert.Equal(2, candidates.Count, "the raw detector does not dedupe by id; that is the discovery service's job");
                Assert.Equal(Path.Combine(versionedRoot, "Soundpacks", "my-pack"), candidates[0], "higher-priority root's pack should come first");
                Assert.Equal(Path.Combine(modRoot, "Soundpacks", "my-pack"), candidates[1], "lower-priority root's pack should come second");
            }
            finally
            {
                Directory.Delete(modRoot, recursive: true);
            }
        }
    }
}
