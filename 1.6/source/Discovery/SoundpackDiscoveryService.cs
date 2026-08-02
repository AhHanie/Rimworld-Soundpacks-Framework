using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Soundpacks_Framework.Models;
using Soundpacks_Framework.Serialization;
using Soundpacks_Framework.Storage;
using Soundpacks_Framework.Validation;
using Verse;

namespace Soundpacks_Framework.Discovery
{
    public static class SoundpackDiscoveryService
    {
        public static List<SoundpackDescriptor> DiscoverAll()
        {
            var descriptors = new List<SoundpackDescriptor>();
            descriptors.AddRange(DiscoverUserPacks());
            descriptors.AddRange(DiscoverModPacks());
            ResolveConflicts(descriptors);
            return descriptors;
        }

        private static IEnumerable<SoundpackDescriptor> DiscoverUserPacks()
        {
            foreach (var id in SoundpackRepository.ListUserPackIds())
            {
                string dir = SoundpackRepository.PackDir(id);
                yield return LoadDescriptor(id, dir, SoundpackSource.User, readOnly: false, null, null, null);
            }
        }

        private static IEnumerable<SoundpackDescriptor> DiscoverModPacks()
        {
            var result = new List<SoundpackDescriptor>();
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                List<string> contentRoots;
                try
                {
                    contentRoots = mod.foldersToLoadDescendingOrder;
                }
                catch
                {
                    continue;
                }
                if (contentRoots == null) continue;

                var seenFolderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var packDir in SoundpackContentDetector.EnumerateCandidatePackDirs(contentRoots))
                {
                    string folderId = Path.GetFileName(packDir);
                    if (!seenFolderIds.Add(folderId)) continue;

                    result.Add(LoadDescriptor(folderId, packDir, SoundpackSource.Mod, readOnly: true,
                        mod.Name, mod.PackageId, mod.ModMetaData?.ModVersion));
                }
            }
            return result;
        }

        private static SoundpackDescriptor LoadDescriptor(string folderId, string dir, SoundpackSource source, bool readOnly,
            string sourceModName, string sourcePackageId, string sourceModVersion)
        {
            var descriptor = new SoundpackDescriptor
            {
                Id = folderId,
                Source = source,
                DirectoryPath = dir,
                ReadOnly = readOnly,
                SourceModName = sourceModName,
                SourcePackageId = sourcePackageId,
                SourceModVersion = sourceModVersion
            };

            string manifestPath = Path.Combine(dir, SoundpackPathPolicy.ManifestFileName);
            try
            {
                string text = File.ReadAllText(manifestPath, System.Text.Encoding.UTF8);
                var parseResult = SoundpackManifestSerializer.Deserialize(text);
                if (!parseResult.Success)
                {
                    descriptor.LoadSucceeded = false;
                    descriptor.LoadError = parseResult.Error;
                    descriptor.Diagnostics.Add(SoundpackDiagnostic.Error("manifest-parse-failed", folderId, manifestPath, parseResult.Error,
                        "Fix or re-export soundpack.json."));
                    return descriptor;
                }

                descriptor.Manifest = parseResult.Manifest;

                if (descriptor.Manifest.id != folderId)
                {
                    descriptor.Diagnostics.Add(SoundpackDiagnostic.Warn("id-folder-mismatch", folderId, manifestPath,
                        "Manifest id '" + descriptor.Manifest.id + "' does not match its folder name '" + folderId + "'",
                        "This pack is discovered and selected by its folder name, not the manifest id field."));
                }

                var structureResult = SoundpackValidator.ValidateStructure(descriptor.Manifest);
                descriptor.Diagnostics.AddRange(structureResult.Diagnostics);
                descriptor.LoadSucceeded = structureResult.StructurallyValid;
                if (!structureResult.StructurallyValid)
                {
                    descriptor.LoadError = "Manifest failed validation; see diagnostics.";
                }
            }
            catch (Exception ex)
            {
                descriptor.LoadSucceeded = false;
                descriptor.LoadError = ex.Message;
                descriptor.Diagnostics.Add(SoundpackDiagnostic.Error("manifest-read-failed", folderId, manifestPath, ex.Message,
                    "Ensure soundpack.json is readable and not corrupted."));
            }

            return descriptor;
        }

        private static void ResolveConflicts(List<SoundpackDescriptor> descriptors)
        {
            var modLoadOrder = LoadedModManager.RunningModsListForReading;

            foreach (var group in descriptors.GroupBy(d => d.Id))
            {
                var members = group.ToList();
                if (members.Count <= 1) continue;

                var userPack = members.FirstOrDefault(d => d.Source == SoundpackSource.User);
                var modPacks = members.Where(d => d.Source == SoundpackSource.Mod).ToList();

                SoundpackDescriptor winner = userPack;
                if (winner == null && modPacks.Count > 0)
                {
                    winner = modPacks
                        .OrderByDescending(d => IndexOfSourceMod(modLoadOrder, d.SourcePackageId))
                        .First();
                }

                foreach (var member in members)
                {
                    if (member == winner) continue;
                    member.SuppressedByConflict = true;
                    member.ConflictNote = winner.Source == SoundpackSource.User
                        ? "A user-installed pack with id '" + member.Id + "' takes precedence over this mod-provided pack."
                        : "Another mod ('" + winner.SourceModName + "') later in the load order provides a pack with the same id.";
                    member.Diagnostics.Add(SoundpackDiagnostic.Warn("duplicate-pack-id", member.Id, member.DirectoryPath,
                        member.ConflictNote, "Rename this pack's id to avoid the collision, or select the winning pack instead."));
                }
            }
        }

        private static int IndexOfSourceMod(List<ModContentPack> modLoadOrder, string packageId)
        {
            for (int i = 0; i < modLoadOrder.Count; i++)
            {
                if (string.Equals(modLoadOrder[i].PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
