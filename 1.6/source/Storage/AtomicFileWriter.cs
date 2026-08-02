using System;
using System.IO;
using System.Text;

namespace Soundpacks_Framework.Storage
{
    public static class AtomicFileWriter
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static void WriteAllText(string destinationPath, string content)
        {
            WriteAllBytes(destinationPath, Utf8NoBom.GetBytes(content));
        }

        public static void WriteAllBytes(string destinationPath, byte[] content)
        {
            string directory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Destination path must include a directory.", nameof(destinationPath));
            }
            Directory.CreateDirectory(directory);

            string tempPath = Path.Combine(directory, Path.GetFileName(destinationPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(flushToDisk: true);
            }

            try
            {
                if (File.Exists(destinationPath))
                {
                    string backupPath = tempPath + ".bak";
                    File.Replace(tempPath, destinationPath, backupPath);
                    File.Delete(backupPath);
                }
                else
                {
                    File.Move(tempPath, destinationPath);
                }
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        public static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
