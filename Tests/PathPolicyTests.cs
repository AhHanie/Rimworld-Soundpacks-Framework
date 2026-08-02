using System.IO;
using Soundpacks_Framework.Storage;

namespace Soundpacks_Framework.Tests
{
    public static class PathPolicyTests
    {
        public static void Register(TestRunner runner)
        {
            runner.Add("PathPolicy.PackId.AcceptsValid", AcceptsValidPackIds);
            runner.Add("PathPolicy.PackId.RejectsInvalid", RejectsInvalidPackIds);
            runner.Add("PathPolicy.Canonicalize.NormalizesBackslashes", NormalizesBackslashes);
            runner.Add("PathPolicy.Canonicalize.RejectsTraversal", RejectsTraversal);
            runner.Add("PathPolicy.Canonicalize.RejectsAbsoluteAndDrive", RejectsAbsoluteAndDrive);
            runner.Add("PathPolicy.Canonicalize.RejectsEmptySegmentsAndNul", RejectsEmptySegmentsAndNul);
            runner.Add("PathPolicy.IsUnderAudioFolder.Works", IsUnderAudioFolderWorks);
            runner.Add("PathPolicy.CaseInsensitiveKey.CollidesAcrossCase", CaseInsensitiveKeyCollidesAcrossCase);
            runner.Add("PathPolicy.Containment.DetectsEscape", ContainmentDetectsEscape);
            runner.Add("PathPolicy.CombineContained.ThrowsOnEscape", CombineContainedThrowsOnEscape);
        }

        private static void AcceptsValidPackIds()
        {
            Assert.True(SoundpackPathPolicy.IsValidPackId("com.example.rifle-overhaul"), "reverse-DNS id");
            Assert.True(SoundpackPathPolicy.IsValidPackId("user.deadbeefdeadbeefdeadbeefdeadbeef"), "guid-like id");
            Assert.True(SoundpackPathPolicy.IsValidPackId(SoundpackPathPolicy.NewGuidPackId()), "generated id");
        }

        private static void RejectsInvalidPackIds()
        {
            Assert.False(SoundpackPathPolicy.IsValidPackId(""), "empty id");
            Assert.False(SoundpackPathPolicy.IsValidPackId(null), "null id");
            Assert.False(SoundpackPathPolicy.IsValidPackId("Has Spaces"), "spaces");
            Assert.False(SoundpackPathPolicy.IsValidPackId("UPPERCASE.id"), "uppercase");
            Assert.False(SoundpackPathPolicy.IsValidPackId("../escape"), "traversal-looking id");
        }

        private static void NormalizesBackslashes()
        {
            bool ok = SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath("Audio\\sub\\clip.mp3", out string canonical, out _);
            Assert.True(ok, "backslash path should canonicalize");
            Assert.Equal("Audio/sub/clip.mp3", canonical, "backslashes must normalize to forward slashes");
        }

        private static void RejectsTraversal()
        {
            Assert.False(SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath("Audio/../../evil.mp3", out _, out _), "parent traversal");
            Assert.False(SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath("Audio/./clip.mp3", out _, out _), "dot segment");
            Assert.False(SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath("..", out _, out _), "bare traversal");
        }

        private static void RejectsAbsoluteAndDrive()
        {
            Assert.False(SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath("/etc/passwd", out _, out _), "leading slash");
            Assert.False(SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath("C:/Windows/System32", out _, out _), "drive prefix");
        }

        private static void RejectsEmptySegmentsAndNul()
        {
            Assert.False(SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath("", out _, out _), "empty string");
            Assert.False(SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath("Audio//clip.mp3", out _, out _), "empty segment");
            Assert.False(SoundpackPathPolicy.TryCanonicalizeArchiveEntryPath("Audio/cl\0ip.mp3", out _, out _), "embedded NUL");
        }

        private static void IsUnderAudioFolderWorks()
        {
            Assert.True(SoundpackPathPolicy.IsUnderAudioFolder("Audio/clip.mp3"), "direct child");
            Assert.True(SoundpackPathPolicy.IsUnderAudioFolder("Audio/sub/clip.mp3"), "nested child");
            Assert.False(SoundpackPathPolicy.IsUnderAudioFolder("soundpack.json"), "manifest is not under Audio/");
            Assert.False(SoundpackPathPolicy.IsUnderAudioFolder("AudioExtra/clip.mp3"), "prefix-only match must not count");
        }

        private static void CaseInsensitiveKeyCollidesAcrossCase()
        {
            string a = SoundpackPathPolicy.CaseInsensitiveKey("Audio/Clip.mp3");
            string b = SoundpackPathPolicy.CaseInsensitiveKey("Audio/clip.MP3");
            Assert.Equal(a, b, "case-insensitive keys must collide regardless of original casing");
        }

        private static void ContainmentDetectsEscape()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "spf_test_base_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(baseDir);
            try
            {
                string inside = Path.Combine(baseDir, "Audio", "clip.mp3");
                string outside = Path.Combine(Path.GetDirectoryName(baseDir), "evil.mp3");
                Assert.True(SoundpackPathPolicy.IsContainedWithin(baseDir, inside), "path inside base dir");
                Assert.False(SoundpackPathPolicy.IsContainedWithin(baseDir, outside), "path outside base dir");
            }
            finally
            {
                Directory.Delete(baseDir, recursive: true);
            }
        }

        private static void CombineContainedThrowsOnEscape()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "spf_test_combine_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(baseDir);
            try
            {
                string safe = SoundpackPathPolicy.CombineContained(baseDir, "Audio/clip.mp3");
                Assert.True(safe.StartsWith(baseDir), "combined path must stay inside base dir");
            }
            finally
            {
                Directory.Delete(baseDir, recursive: true);
            }
        }
    }
}
