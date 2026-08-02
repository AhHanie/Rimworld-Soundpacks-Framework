using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Soundpacks_Framework.Models;
using Soundpacks_Framework.Serialization;
using Verse;

namespace Soundpacks_Framework.Storage
{
    public sealed class PackLoadResult
    {
        public bool Success;
        public SoundpackManifest Manifest;
        public string Error;
    }

    public static class SoundpackRepository
    {
        public static string RootPath =>
            Path.Combine(GenFilePaths.SaveDataFolderPath, "SoundpacksFramework");

        public static string PacksDir => Path.Combine(RootPath, "Packs");
        public static string StagingDir => Path.Combine(RootPath, "Staging");
        public static string BackupsDir => Path.Combine(RootPath, "Backups");
        public static string DraftsDir => Path.Combine(RootPath, "Drafts");
        public static string RecoveryDir => Path.Combine(RootPath, "Recovery");

        public static string PackDir(string packId) => Path.Combine(PacksDir, packId);
        public static string PackManifestPath(string packId) => Path.Combine(PackDir(packId), SoundpackPathPolicy.ManifestFileName);
        public static string PackAudioDir(string packId) => Path.Combine(PackDir(packId), "Audio");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(PacksDir);
            Directory.CreateDirectory(StagingDir);
            Directory.CreateDirectory(BackupsDir);
            Directory.CreateDirectory(DraftsDir);
            Directory.CreateDirectory(RecoveryDir);
        }

        public static void RecoverOnStartup()
        {
            EnsureDirectories();

            if (Directory.Exists(PacksDir))
            {
                foreach (var packDir in Directory.GetDirectories(PacksDir))
                {
                    RecoverPackDirectory(packDir);
                }
            }

            WipeChildDirectories(StagingDir);
            WipeChildDirectories(DraftsDir);
        }

        private static void RecoverPackDirectory(string packDir)
        {
            string finalManifest = Path.Combine(packDir, SoundpackPathPolicy.ManifestFileName);
            string[] tmpFiles = Directory.Exists(packDir)
                ? Directory.GetFiles(packDir, SoundpackPathPolicy.ManifestFileName + ".*.tmp")
                : Array.Empty<string>();

            if (tmpFiles.Length == 0)
            {
                return;
            }

            if (File.Exists(finalManifest))
            {
                foreach (var tmp in tmpFiles) AtomicFileWriter.TryDelete(tmp);
                return;
            }

            string best = tmpFiles.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            bool recovered = false;
            foreach (var tmp in tmpFiles)
            {
                if (!recovered && tmp == best)
                {
                    try
                    {
                        string text = File.ReadAllText(tmp, System.Text.Encoding.UTF8);
                        var result = SoundpackManifestSerializer.Deserialize(text);
                        if (result.Success)
                        {
                            File.Move(tmp, finalManifest);
                            recovered = true;
                            Logger.Warning("Recovered manifest for pack directory '" + packDir + "' from a crash-interrupted write.");
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Exception(ex, "Failed recovering manifest temp file " + tmp);
                    }
                }
                AtomicFileWriter.TryDelete(tmp);
            }
        }

        private static void WipeChildDirectories(string parent)
        {
            if (!Directory.Exists(parent)) return;
            foreach (var dir in Directory.GetDirectories(parent))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "Failed wiping stale directory " + dir);
                }
            }
        }

        public static IEnumerable<string> ListUserPackIds()
        {
            if (!Directory.Exists(PacksDir)) yield break;
            foreach (var dir in Directory.GetDirectories(PacksDir))
            {
                if (File.Exists(Path.Combine(dir, SoundpackPathPolicy.ManifestFileName)))
                {
                    yield return Path.GetFileName(dir);
                }
            }
        }

        public static PackLoadResult LoadPack(string packId)
        {
            string manifestPath = PackManifestPath(packId);
            if (!File.Exists(manifestPath))
            {
                return new PackLoadResult { Success = false, Error = "No soundpack.json found for pack '" + packId + "'." };
            }
            try
            {
                string text = File.ReadAllText(manifestPath, System.Text.Encoding.UTF8);
                var result = SoundpackManifestSerializer.Deserialize(text);
                if (!result.Success)
                {
                    return new PackLoadResult { Success = false, Error = result.Error };
                }
                return new PackLoadResult { Success = true, Manifest = result.Manifest };
            }
            catch (Exception ex)
            {
                return new PackLoadResult { Success = false, Error = ex.Message };
            }
        }

        public static void SavePackManifest(SoundpackManifest manifest)
        {
            if (string.IsNullOrEmpty(manifest.id) || !SoundpackPathPolicy.IsValidPackId(manifest.id))
            {
                throw new ArgumentException("Manifest has an invalid or missing id.", nameof(manifest));
            }

            EnsureDirectories();
            string manifestPath = PackManifestPath(manifest.id);

            if (File.Exists(manifestPath))
            {
                BackupPack(manifest.id, "presave");
            }

            Directory.CreateDirectory(PackDir(manifest.id));
            Directory.CreateDirectory(PackAudioDir(manifest.id));

            string json = SoundpackManifestSerializer.SerializeCanonical(manifest);
            AtomicFileWriter.WriteAllText(manifestPath, json);
        }

        public static string BackupPack(string packId, string reason)
        {
            string source = PackDir(packId);
            if (!Directory.Exists(source))
            {
                return null;
            }
            string backupDir = Path.Combine(BackupsDir, packId + "-" + DateTime.UtcNow.Ticks + "-" + reason);
            CopyDirectoryRecursive(source, backupDir);
            return backupDir;
        }

        public static void DeletePack(string packId)
        {
            string source = PackDir(packId);
            if (!Directory.Exists(source)) return;

            string backupDir = Path.Combine(BackupsDir, packId + "-" + DateTime.UtcNow.Ticks + "-deleted");
            Directory.CreateDirectory(BackupsDir);
            Directory.Move(source, backupDir);
        }

        public static string NewStagingDir()
        {
            EnsureDirectories();
            string dir = Path.Combine(StagingDir, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string NewDraftDir()
        {
            EnsureDirectories();
            string dir = Path.Combine(DraftsDir, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void DeleteScratchDirectory(string path)
        {
            string full = Path.GetFullPath(path);
            bool isScratch = SoundpackPathPolicy.IsContainedWithin(StagingDir, full) ||
                              SoundpackPathPolicy.IsContainedWithin(DraftsDir, full);
            if (!isScratch)
            {
                throw new InvalidOperationException("Refusing to delete a directory outside Staging/Drafts: " + full);
            }
            if (Directory.Exists(full))
            {
                Directory.Delete(full, recursive: true);
            }
        }

        public static void PromoteDirectoryToPack(string sourceDir, string packId)
        {
            if (!SoundpackPathPolicy.IsValidPackId(packId))
            {
                throw new ArgumentException("Invalid pack id: " + packId, nameof(packId));
            }

            EnsureDirectories();
            string destination = PackDir(packId);

            if (Directory.Exists(destination))
            {
                BackupPack(packId, "replaced");
                Directory.Delete(destination, recursive: true);
            }

            Directory.CreateDirectory(PacksDir);
            Directory.Move(sourceDir, destination);
        }

        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectoryRecursive(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }
    }
}
