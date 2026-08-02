using System.Linq;
using Soundpacks_Framework.Serialization;
using Soundpacks_Framework.Serialization.Json;

namespace Soundpacks_Framework.Tests
{
    public static class ManifestSerializerTests
    {
        private const string ValidManifest = @"{
            ""schemaVersion"": 1,
            ""id"": ""com.example.pack"",
            ""name"": ""Example Pack"",
            ""author"": ""Example Author"",
            ""version"": ""1.0.0"",
            ""supportedRimWorldVersions"": [""1.6""],
            ""mappings"": [
                {
                    ""soundDef"": ""Gun_ShootRifle"",
                    ""subSoundName"": ""Gun_ShootRifle_0"",
                    ""subSoundIndex"": 0,
                    ""files"": [ { ""path"": ""Audio/a.mp3"" }, { ""path"": ""Audio/b.mp3"" } ]
                }
            ]
        }";

        public static void Register(TestRunner runner)
        {
            runner.Add("Manifest.Deserialize.ValidManifest", DeserializeValid);
            runner.Add("Manifest.RoundTrip.PreservesUnknownRootField", PreservesUnknownRootField);
            runner.Add("Manifest.RoundTrip.PreservesUnknownMappingField", PreservesUnknownMappingField);
            runner.Add("Manifest.Migrate.RejectsNewerSchema", RejectsNewerSchema);
            runner.Add("Manifest.Migrate.RejectsMissingSchema", RejectsMissingSchema);
            runner.Add("Manifest.Deserialize.RejectsMalformedJson", RejectsMalformedJson);
            runner.Add("Manifest.TargetKey.DistinguishesTargets", TargetKeyDistinguishesTargets);
            runner.Add("Manifest.Volume.AbsentDefaultsToUnityAndOmitsOnSave", VolumeAbsentDefaultsToUnityAndOmitsOnSave);
            runner.Add("Manifest.Volume.NonDefaultRoundTrips", VolumeNonDefaultRoundTrips);
            runner.Add("Manifest.Volume.UnknownFileFieldStillRoundTrips", VolumeUnknownFileFieldStillRoundTrips);
            runner.Add("Manifest.Volume.MalformedIsRetainedAsInvalid", VolumeMalformedIsRetainedAsInvalid);
            runner.Add("Manifest.VolumeRange.AbsentIsNullAndOmittedOnSave", VolumeRangeAbsentIsNullAndOmittedOnSave);
            runner.Add("Manifest.VolumeRange.ValidRoundTrips", VolumeRangeValidRoundTrips);
            runner.Add("Manifest.VolumeRange.UnknownFieldStillRoundTrips", VolumeRangeUnknownFieldStillRoundTrips);
            runner.Add("Manifest.VolumeRange.MalformedIsRetainedAsInvalid", VolumeRangeMalformedIsRetainedAsInvalid);
        }

        private static void DeserializeValid()
        {
            var result = SoundpackManifestSerializer.Deserialize(ValidManifest);
            Assert.True(result.Success, "expected valid manifest to deserialize: " + result.Error);
            Assert.Equal("com.example.pack", result.Manifest.id, "id");
            Assert.Equal(1, result.Manifest.mappings.Count, "mapping count");
            Assert.Equal(2, result.Manifest.mappings[0].files.Count, "file count");
        }

        private static void PreservesUnknownRootField()
        {
            string withExtra = ValidManifest.Substring(0, ValidManifest.LastIndexOf('}')) + ", \"futureField\": \"keepme\" }";
            var result = SoundpackManifestSerializer.Deserialize(withExtra);
            Assert.True(result.Success, "expected manifest with unknown field to still deserialize: " + result.Error);

            string resaved = SoundpackManifestSerializer.SerializeCanonical(result.Manifest);
            var reparsed = JsonParser.Parse(resaved);
            Assert.Equal("keepme", reparsed.Get("futureField").AsString(), "unknown root field must round-trip");
        }

        private static void PreservesUnknownMappingField()
        {
            string withExtra = ValidManifest.Replace("\"soundDef\": \"Gun_ShootRifle\",", "\"soundDef\": \"Gun_ShootRifle\", \"toolHint\": \"editor-only\",");
            var result = SoundpackManifestSerializer.Deserialize(withExtra);
            Assert.True(result.Success, "expected manifest with unknown mapping field to deserialize: " + result.Error);

            string resaved = SoundpackManifestSerializer.SerializeCanonical(result.Manifest);
            var reparsed = JsonParser.Parse(resaved);
            string toolHint = reparsed.Get("mappings").ArrayItems[0].Get("toolHint").AsString();
            Assert.Equal("editor-only", toolHint, "unknown mapping field must round-trip");
        }

        private static void RejectsNewerSchema()
        {
            string futureSchema = ValidManifest.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 999,");
            var result = SoundpackManifestSerializer.Deserialize(futureSchema);
            Assert.False(result.Success, "a manifest from a newer schema version must not deserialize");
        }

        private static void RejectsMissingSchema()
        {
            const string noSchema = "{\"id\":\"x\",\"mappings\":[]}";
            var result = SoundpackManifestSerializer.Deserialize(noSchema);
            Assert.False(result.Success, "a manifest without schemaVersion must be rejected");
        }

        private static void RejectsMalformedJson()
        {
            var result = SoundpackManifestSerializer.Deserialize("{ this is not json");
            Assert.False(result.Success, "malformed JSON must fail with a diagnostic, not throw");
        }

        private static void TargetKeyDistinguishesTargets()
        {
            var result = SoundpackManifestSerializer.Deserialize(ValidManifest);
            var mapping = result.Manifest.mappings[0];
            var clone = mapping.Clone();
            Assert.Equal(mapping.TargetKey(), clone.TargetKey(), "identical targets must produce equal keys");

            clone.subSoundIndex = 1;
            Assert.False(mapping.TargetKey() == clone.TargetKey(), "different sub-sound index must change the key");

            var sameNamePrefix = mapping.Clone();
            sameNamePrefix.soundDef = "Gun_ShootRifle_0";
            sameNamePrefix.subSoundName = null;
            sameNamePrefix.subSoundIndex = mapping.subSoundIndex;
            Assert.False(mapping.TargetKey() == sameNamePrefix.TargetKey(),
                "concatenating soundDef+subSoundName without a separator must not collide across the field boundary");
        }

        private static void VolumeAbsentDefaultsToUnityAndOmitsOnSave()
        {
            var result = SoundpackManifestSerializer.Deserialize(ValidManifest);
            Assert.True(result.Success, "expected valid manifest to deserialize: " + result.Error);
            var file = result.Manifest.mappings[0].files[0];
            Assert.Equal(1f, file.volume, "absent volume must default to 1.0");
            Assert.False(file.volumeMalformed, "absent volume must not be malformed");

            string resaved = SoundpackManifestSerializer.SerializeCanonical(result.Manifest);
            var reparsed = JsonParser.Parse(resaved);
            var fileJson = reparsed.Get("mappings").ArrayItems[0].Get("files").ArrayItems[0];
            Assert.False(fileJson.Has("volume"), "canonical serialization must omit volume at the default 1.0");
        }

        private static void VolumeNonDefaultRoundTrips()
        {
            string withVolumes = ValidManifest.Replace(
                @"""files"": [ { ""path"": ""Audio/a.mp3"" }, { ""path"": ""Audio/b.mp3"" } ]",
                @"""files"": [ { ""path"": ""Audio/a.mp3"", ""volume"": 0.5 }, { ""path"": ""Audio/b.mp3"", ""volume"": 2.0 } ]");
            var result = SoundpackManifestSerializer.Deserialize(withVolumes);
            Assert.True(result.Success, "expected manifest with volumes to deserialize: " + result.Error);
            var files = result.Manifest.mappings[0].files;
            Assert.Equal(0.5f, files[0].volume, "first file volume");
            Assert.Equal(2.0f, files[1].volume, "second file volume");

            var clone = result.Manifest.Clone();
            Assert.Equal(0.5f, clone.mappings[0].files[0].volume, "clone must preserve first file volume");
            Assert.Equal(2.0f, clone.mappings[0].files[1].volume, "clone must preserve second file volume");

            string resaved = SoundpackManifestSerializer.SerializeCanonical(clone);
            var reparsed = SoundpackManifestSerializer.Deserialize(resaved);
            Assert.True(reparsed.Success, "resaved manifest must reparse: " + reparsed.Error);
            Assert.Equal(0.5f, reparsed.Manifest.mappings[0].files[0].volume, "reparsed first file volume");
            Assert.Equal(2.0f, reparsed.Manifest.mappings[0].files[1].volume, "reparsed second file volume");
        }

        private static void VolumeUnknownFileFieldStillRoundTrips()
        {
            string withExtra = ValidManifest.Replace(
                @"{ ""path"": ""Audio/a.mp3"" }",
                @"{ ""path"": ""Audio/a.mp3"", ""volume"": 0.5, ""toolHint"": ""editor-only"" }");
            var result = SoundpackManifestSerializer.Deserialize(withExtra);
            Assert.True(result.Success, "expected manifest with unknown file field to deserialize: " + result.Error);
            Assert.Equal(0.5f, result.Manifest.mappings[0].files[0].volume, "volume must still be parsed as a known field");

            string resaved = SoundpackManifestSerializer.SerializeCanonical(result.Manifest);
            var reparsed = JsonParser.Parse(resaved);
            var fileJson = reparsed.Get("mappings").ArrayItems[0].Get("files").ArrayItems[0];
            Assert.Equal("editor-only", fileJson.Get("toolHint").AsString(), "unknown file field must round-trip");
            Assert.Equal(0.5, fileJson.Get("volume").AsNumber(), "volume must round-trip in the resaved JSON");
        }

        private static void VolumeMalformedIsRetainedAsInvalid()
        {
            string withStringVolume = ValidManifest.Replace(
                @"{ ""path"": ""Audio/a.mp3"" }",
                @"{ ""path"": ""Audio/a.mp3"", ""volume"": ""loud"" }");
            var stringResult = SoundpackManifestSerializer.Deserialize(withStringVolume);
            Assert.True(stringResult.Success, "a non-numeric volume must still deserialize the manifest: " + stringResult.Error);
            var stringFile = stringResult.Manifest.mappings[0].files[0];
            Assert.True(stringFile.volumeMalformed, "a string volume must be flagged malformed, not coerced to the default");
            Assert.Equal(1f, stringFile.volume, "malformed volume must not silently overwrite the model default field");

            string withNullVolume = ValidManifest.Replace(
                @"{ ""path"": ""Audio/a.mp3"" }",
                @"{ ""path"": ""Audio/a.mp3"", ""volume"": null }");
            var nullResult = SoundpackManifestSerializer.Deserialize(withNullVolume);
            Assert.True(nullResult.Success, "an explicit null volume must still deserialize the manifest: " + nullResult.Error);
            Assert.True(nullResult.Manifest.mappings[0].files[0].volumeMalformed, "an explicit null volume must be flagged malformed");

            string withOverflowVolume = ValidManifest.Replace(
                @"{ ""path"": ""Audio/a.mp3"" }",
                @"{ ""path"": ""Audio/a.mp3"", ""volume"": 1e400 }");
            var overflowResult = SoundpackManifestSerializer.Deserialize(withOverflowVolume);
            Assert.False(overflowResult.Success,
                "an exponent this extreme is unparsable as a number token on the .NET Framework 4.7.2 CLR, so the whole manifest must be rejected as malformed JSON rather than silently coerced");
        }

        private static void VolumeRangeAbsentIsNullAndOmittedOnSave()
        {
            var result = SoundpackManifestSerializer.Deserialize(ValidManifest);
            Assert.True(result.Success, "expected valid manifest to deserialize: " + result.Error);
            var mapping = result.Manifest.mappings[0];
            Assert.Null(mapping.volumeRange, "absent volumeRange must deserialize to null, not a default range");
            Assert.False(mapping.volumeRangeMalformed, "absent volumeRange must not be malformed");

            string resaved = SoundpackManifestSerializer.SerializeCanonical(result.Manifest);
            var reparsed = JsonParser.Parse(resaved);
            var mappingJson = reparsed.Get("mappings").ArrayItems[0];
            Assert.False(mappingJson.Has("volumeRange"), "canonical serialization must omit volumeRange entirely when absent");
        }

        private static void VolumeRangeValidRoundTrips()
        {
            string withRange = ValidManifest.Replace(
                @"""subSoundIndex"": 0,",
                @"""subSoundIndex"": 0, ""volumeRange"": { ""min"": 40, ""max"": 70 },");
            var result = SoundpackManifestSerializer.Deserialize(withRange);
            Assert.True(result.Success, "expected manifest with volumeRange to deserialize: " + result.Error);
            var mapping = result.Manifest.mappings[0];
            Assert.NotNull(mapping.volumeRange, "volumeRange must be populated");
            Assert.Equal(40f, mapping.volumeRange.min, "volumeRange.min");
            Assert.Equal(70f, mapping.volumeRange.max, "volumeRange.max");
            Assert.False(mapping.volumeRangeMalformed, "a valid volumeRange must not be flagged malformed");

            var clone = result.Manifest.Clone();
            Assert.NotNull(clone.mappings[0].volumeRange, "clone must preserve volumeRange");
            Assert.Equal(40f, clone.mappings[0].volumeRange.min, "clone volumeRange.min");
            Assert.Equal(70f, clone.mappings[0].volumeRange.max, "clone volumeRange.max");

            string resaved = SoundpackManifestSerializer.SerializeCanonical(clone);
            var reparsed = SoundpackManifestSerializer.Deserialize(resaved);
            Assert.True(reparsed.Success, "resaved manifest must reparse: " + reparsed.Error);
            Assert.NotNull(reparsed.Manifest.mappings[0].volumeRange, "reparsed volumeRange must not be null");
            Assert.Equal(40f, reparsed.Manifest.mappings[0].volumeRange.min, "reparsed volumeRange.min");
            Assert.Equal(70f, reparsed.Manifest.mappings[0].volumeRange.max, "reparsed volumeRange.max");
        }

        private static void VolumeRangeUnknownFieldStillRoundTrips()
        {
            string withExtra = ValidManifest.Replace(
                @"""subSoundIndex"": 0,",
                @"""subSoundIndex"": 0, ""volumeRange"": { ""min"": 40, ""max"": 70, ""toolHint"": ""editor-only"" },");
            var result = SoundpackManifestSerializer.Deserialize(withExtra);
            Assert.True(result.Success, "expected manifest with unknown volumeRange field to deserialize: " + result.Error);
            Assert.Equal(40f, result.Manifest.mappings[0].volumeRange.min, "volumeRange.min must still be parsed as a known field");

            string resaved = SoundpackManifestSerializer.SerializeCanonical(result.Manifest);
            var reparsed = JsonParser.Parse(resaved);
            var volumeRangeJson = reparsed.Get("mappings").ArrayItems[0].Get("volumeRange");
            Assert.Equal("editor-only", volumeRangeJson.Get("toolHint").AsString(), "unknown volumeRange field must round-trip");
            Assert.Equal(40.0, volumeRangeJson.Get("min").AsNumber(), "min must round-trip in the resaved JSON");
        }

        private static void VolumeRangeMalformedIsRetainedAsInvalid()
        {
            string withStringMin = ValidManifest.Replace(
                @"""subSoundIndex"": 0,",
                @"""subSoundIndex"": 0, ""volumeRange"": { ""min"": ""loud"", ""max"": 70 },");
            var stringResult = SoundpackManifestSerializer.Deserialize(withStringMin);
            Assert.True(stringResult.Success, "a non-numeric volumeRange.min must still deserialize the manifest: " + stringResult.Error);
            var stringMapping = stringResult.Manifest.mappings[0];
            Assert.True(stringMapping.volumeRangeMalformed, "a string volumeRange.min must be flagged malformed, not coerced");
            Assert.Null(stringMapping.volumeRange, "malformed volumeRange must not be salvaged into a partial value");

            string withMissingMax = ValidManifest.Replace(
                @"""subSoundIndex"": 0,",
                @"""subSoundIndex"": 0, ""volumeRange"": { ""min"": 40 },");
            var missingResult = SoundpackManifestSerializer.Deserialize(withMissingMax);
            Assert.True(missingResult.Success, "a volumeRange missing max must still deserialize the manifest: " + missingResult.Error);
            Assert.True(missingResult.Manifest.mappings[0].volumeRangeMalformed, "a volumeRange missing max must be flagged malformed");

            string withArrayShape = ValidManifest.Replace(
                @"""subSoundIndex"": 0,",
                @"""subSoundIndex"": 0, ""volumeRange"": [40, 70],");
            var arrayResult = SoundpackManifestSerializer.Deserialize(withArrayShape);
            Assert.True(arrayResult.Success, "a volumeRange that is an array instead of an object must still deserialize the manifest: " + arrayResult.Error);
            Assert.True(arrayResult.Manifest.mappings[0].volumeRangeMalformed, "a non-object volumeRange must be flagged malformed");

            string resaved = SoundpackManifestSerializer.SerializeCanonical(stringResult.Manifest);
            var reparsed = JsonParser.Parse(resaved);
            Assert.False(reparsed.Get("mappings").ArrayItems[0].Has("volumeRange"), "a malformed volumeRange must never be round-tripped on save");
        }
    }
}
