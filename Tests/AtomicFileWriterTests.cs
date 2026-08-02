using System;
using System.IO;
using System.Linq;
using Soundpacks_Framework.Storage;

namespace Soundpacks_Framework.Tests
{
    public static class AtomicFileWriterTests
    {
        public static void Register(TestRunner runner)
        {
            runner.Add("AtomicFileWriter.Write.RoundTrips", WriteRoundTrips);
            runner.Add("AtomicFileWriter.Write.OverwritesAndLeavesNoTempFiles", OverwriteLeavesNoTempFiles);
        }

        private static void WriteRoundTrips()
        {
            string dir = Path.Combine(Path.GetTempPath(), "spf_test_atomic_" + Guid.NewGuid().ToString("N"));
            string file = Path.Combine(dir, "soundpack.json");
            try
            {
                AtomicFileWriter.WriteAllText(file, "{\"a\":1}");
                Assert.True(File.Exists(file), "destination file must exist after write");
                Assert.Equal("{\"a\":1}", File.ReadAllText(file), "content must match what was written");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }

        private static void OverwriteLeavesNoTempFiles()
        {
            string dir = Path.Combine(Path.GetTempPath(), "spf_test_atomic2_" + Guid.NewGuid().ToString("N"));
            string file = Path.Combine(dir, "soundpack.json");
            try
            {
                AtomicFileWriter.WriteAllText(file, "{\"a\":1}");
                AtomicFileWriter.WriteAllText(file, "{\"a\":2}");

                Assert.Equal("{\"a\":2}", File.ReadAllText(file), "second write must win");

                var leftovers = Directory.GetFiles(dir).Where(f => f.EndsWith(".tmp") || f.EndsWith(".bak")).ToList();
                Assert.Equal(0, leftovers.Count, "no temp/backup files should remain after a successful atomic write");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
        }
    }
}
