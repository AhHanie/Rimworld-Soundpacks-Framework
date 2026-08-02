using System;
using System.Collections.Generic;
using Soundpacks_Framework.Models;
using Soundpacks_Framework.Serialization.Json;

namespace Soundpacks_Framework.Serialization
{
    public sealed class ManifestMigrationResult
    {
        public bool Success;
        public string Error;
        public JsonValue Root;
        public List<string> Notes = new List<string>();

        public static ManifestMigrationResult Fail(string error) => new ManifestMigrationResult { Success = false, Error = error };
    }

    public static class ManifestMigrator
    {
        private static readonly Dictionary<int, Action<JsonValue, List<string>>> Steps =
            new Dictionary<int, Action<JsonValue, List<string>>>
            {
            };

        public static ManifestMigrationResult MigrateToCurrent(JsonValue root)
        {
            int version = root.Get("schemaVersion").AsInt(0);
            if (version <= 0)
            {
                return ManifestMigrationResult.Fail("Manifest is missing a valid schemaVersion.");
            }
            if (version > SoundpackManifest.CurrentSchemaVersion)
            {
                return ManifestMigrationResult.Fail(
                    $"Manifest schemaVersion {version} is newer than the {SoundpackManifest.CurrentSchemaVersion} this framework version supports. " +
                    "Update Soundpacks Framework to use this pack.");
            }

            var notes = new List<string>();
            int current = version;
            while (current < SoundpackManifest.CurrentSchemaVersion)
            {
                if (!Steps.TryGetValue(current, out var step))
                {
                    return ManifestMigrationResult.Fail($"No migration path registered from schemaVersion {current}.");
                }
                step(root, notes);
                current++;
                root.Set("schemaVersion", JsonValue.Of(current));
                notes.Add($"Migrated manifest from schemaVersion {current - 1} to {current}.");
            }

            return new ManifestMigrationResult { Success = true, Root = root, Notes = notes };
        }
    }
}
