using System.Collections.Generic;
using Soundpacks_Framework.Models;
using Soundpacks_Framework.Serialization.Json;

namespace Soundpacks_Framework.Serialization
{
    public sealed class ManifestDeserializeResult
    {
        public SoundpackManifest Manifest;
        public bool Success;
        public string Error;
        public List<string> MigrationNotes = new List<string>();

        public static ManifestDeserializeResult Fail(string error) =>
            new ManifestDeserializeResult { Success = false, Error = error };
    }

    public static class SoundpackManifestSerializer
    {
        private static readonly string[] KnownRootKeys =
        {
            "schemaVersion", "id", "name", "author", "description", "version",
            "supportedRimWorldVersions", "attribution", "license", "mappings"
        };

        private static readonly string[] KnownMappingKeys =
        {
            "soundDef", "expectedSourcePackageId", "subSoundName", "subSoundIndex", "files", "volumeRange"
        };

        private static readonly string[] KnownFileKeys = { "path", "volume" };

        private static readonly string[] KnownVolumeRangeKeys = { "min", "max" };

        public static ManifestDeserializeResult Deserialize(string json)
        {
            if (!JsonParser.TryParse(json, out JsonValue root, out string parseError))
            {
                return ManifestDeserializeResult.Fail("Malformed JSON: " + parseError);
            }
            if (root.Kind != JsonKind.Object)
            {
                return ManifestDeserializeResult.Fail("Manifest root must be a JSON object.");
            }

            var migration = ManifestMigrator.MigrateToCurrent(root);
            if (!migration.Success)
            {
                return ManifestDeserializeResult.Fail(migration.Error);
            }
            root = migration.Root;

            var manifest = new SoundpackManifest
            {
                schemaVersion = root.Get("schemaVersion").AsInt(SoundpackManifest.CurrentSchemaVersion),
                id = root.Get("id").AsString(),
                name = root.Get("name").AsString(),
                author = root.Get("author").AsString(),
                description = root.Get("description").AsString(),
                version = root.Get("version").AsString(),
                attribution = root.Get("attribution").AsString(),
                license = root.Get("license").AsString(),
            };

            foreach (var item in root.Get("supportedRimWorldVersions").ArrayItems)
            {
                var s = item.AsString();
                if (s != null) manifest.supportedRimWorldVersions.Add(s);
            }

            foreach (var mappingJson in root.Get("mappings").ArrayItems)
            {
                if (mappingJson.Kind != JsonKind.Object) continue;
                manifest.mappings.Add(DeserializeMapping(mappingJson));
            }

            manifest.extensionData = ExtractExtension(root, KnownRootKeys);

            return new ManifestDeserializeResult
            {
                Success = true,
                Manifest = manifest,
                MigrationNotes = migration.Notes
            };
        }

        private static SoundpackMapping DeserializeMapping(JsonValue mappingJson)
        {
            var mapping = new SoundpackMapping
            {
                soundDef = mappingJson.Get("soundDef").AsString(),
                expectedSourcePackageId = mappingJson.Get("expectedSourcePackageId").AsString(),
                subSoundName = mappingJson.Get("subSoundName").AsString(),
                subSoundIndex = mappingJson.Get("subSoundIndex").AsInt(-1),
            };
            foreach (var fileJson in mappingJson.Get("files").ArrayItems)
            {
                if (fileJson.Kind != JsonKind.Object) continue;
                mapping.files.Add(DeserializeFile(fileJson));
            }

            if (mappingJson.Has("volumeRange"))
            {
                var volumeRangeJson = mappingJson.Get("volumeRange");
                double min = volumeRangeJson.Get("min").AsNumber();
                double max = volumeRangeJson.Get("max").AsNumber();
                bool minOk = volumeRangeJson.Get("min").Kind == JsonKind.Number && !double.IsNaN(min) && !double.IsInfinity(min);
                bool maxOk = volumeRangeJson.Get("max").Kind == JsonKind.Number && !double.IsNaN(max) && !double.IsInfinity(max);
                if (volumeRangeJson.Kind == JsonKind.Object && minOk && maxOk)
                {
                    mapping.volumeRange = new SoundpackVolumeRange
                    {
                        min = (float)min,
                        max = (float)max,
                        extensionData = ExtractExtension(volumeRangeJson, KnownVolumeRangeKeys)
                    };
                }
                else
                {
                    mapping.volumeRangeMalformed = true;
                }
            }

            mapping.extensionData = ExtractExtension(mappingJson, KnownMappingKeys);
            return mapping;
        }

        private static SoundpackAudioFile DeserializeFile(JsonValue fileJson)
        {
            var file = new SoundpackAudioFile { path = fileJson.Get("path").AsString() };

            if (fileJson.Has("volume"))
            {
                var volumeJson = fileJson.Get("volume");
                double volume = volumeJson.AsNumber();
                if (volumeJson.Kind == JsonKind.Number && !double.IsNaN(volume) && !double.IsInfinity(volume))
                {
                    file.volume = (float)volume;
                }
                else
                {
                    file.volumeMalformed = true;
                }
            }

            file.extensionData = ExtractExtension(fileJson, KnownFileKeys);
            return file;
        }

        private static JsonValue ExtractExtension(JsonValue objectValue, string[] knownKeys)
        {
            var remainder = objectValue.DeepClone();
            foreach (var key in knownKeys)
            {
                remainder.Remove(key);
            }
            return remainder.ObjectMembers.Count > 0 ? remainder : null;
        }

        public static string SerializeCanonical(SoundpackManifest manifest)
        {
            return JsonWriter.Write(ToJson(manifest), canonical: true, indent: true);
        }

        public static JsonValue ToJson(SoundpackManifest manifest)
        {
            var root = manifest.extensionData?.DeepClone() ?? JsonValue.NewObject();

            root.Set("schemaVersion", JsonValue.Of(manifest.schemaVersion == 0 ? SoundpackManifest.CurrentSchemaVersion : manifest.schemaVersion));
            root.Set("id", JsonValue.Of(manifest.id));
            root.Set("name", JsonValue.Of(manifest.name));
            root.Set("author", JsonValue.Of(manifest.author));
            root.Set("description", JsonValue.Of(manifest.description));
            root.Set("version", JsonValue.Of(manifest.version));
            root.Set("attribution", JsonValue.Of(manifest.attribution));
            root.Set("license", JsonValue.Of(manifest.license));

            var versions = JsonValue.NewArray();
            foreach (var v in manifest.supportedRimWorldVersions)
            {
                versions.Add(JsonValue.Of(v));
            }
            root.Set("supportedRimWorldVersions", versions);

            var mappings = JsonValue.NewArray();
            foreach (var mapping in manifest.mappings)
            {
                mappings.Add(MappingToJson(mapping));
            }
            root.Set("mappings", mappings);

            return root;
        }

        private static JsonValue MappingToJson(SoundpackMapping mapping)
        {
            var obj = mapping.extensionData?.DeepClone() ?? JsonValue.NewObject();
            obj.Set("soundDef", JsonValue.Of(mapping.soundDef));
            if (!string.IsNullOrEmpty(mapping.expectedSourcePackageId))
            {
                obj.Set("expectedSourcePackageId", JsonValue.Of(mapping.expectedSourcePackageId));
            }
            obj.Set("subSoundName", JsonValue.Of(mapping.subSoundName));
            obj.Set("subSoundIndex", JsonValue.Of(mapping.subSoundIndex));

            var files = JsonValue.NewArray();
            foreach (var file in mapping.files)
            {
                var fileObj = file.extensionData?.DeepClone() ?? JsonValue.NewObject();
                fileObj.Set("path", JsonValue.Of(file.path));
                if (!file.volumeMalformed && file.volume != 1f)
                {
                    fileObj.Set("volume", JsonValue.Of((double)file.volume));
                }
                files.Add(fileObj);
            }
            obj.Set("files", files);

            if (mapping.volumeRange != null && !mapping.volumeRangeMalformed)
            {
                var volumeRangeObj = mapping.volumeRange.extensionData?.DeepClone() ?? JsonValue.NewObject();
                volumeRangeObj.Set("min", JsonValue.Of((double)mapping.volumeRange.min));
                volumeRangeObj.Set("max", JsonValue.Of((double)mapping.volumeRange.max));
                obj.Set("volumeRange", volumeRangeObj);
            }

            return obj;
        }
    }
}
