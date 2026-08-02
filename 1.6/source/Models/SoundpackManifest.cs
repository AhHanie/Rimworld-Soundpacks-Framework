using System.Collections.Generic;
using Soundpacks_Framework.Serialization.Json;

namespace Soundpacks_Framework.Models
{
    public sealed class SoundpackManifest
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion;
        public string id;
        public string name;
        public string author;
        public string description;
        public string version;
        public List<string> supportedRimWorldVersions = new List<string>();
        public string attribution;
        public string license;
        public List<SoundpackMapping> mappings = new List<SoundpackMapping>();

        public JsonValue extensionData;

        public SoundpackManifest Clone()
        {
            var clone = new SoundpackManifest
            {
                schemaVersion = schemaVersion,
                id = id,
                name = name,
                author = author,
                description = description,
                version = version,
                supportedRimWorldVersions = new List<string>(supportedRimWorldVersions),
                attribution = attribution,
                license = license,
                mappings = new List<SoundpackMapping>(mappings.Count),
                extensionData = extensionData?.DeepClone()
            };
            foreach (var mapping in mappings)
            {
                clone.mappings.Add(mapping.Clone());
            }
            return clone;
        }
    }

    public sealed class SoundpackMapping
    {
        public string soundDef;
        public string expectedSourcePackageId;
        public string subSoundName;
        public int subSoundIndex = -1;
        public List<SoundpackAudioFile> files = new List<SoundpackAudioFile>();
        public SoundpackVolumeRange volumeRange;
        public bool volumeRangeMalformed;

        public JsonValue extensionData;

        public SoundpackMapping Clone()
        {
            var clone = new SoundpackMapping
            {
                soundDef = soundDef,
                expectedSourcePackageId = expectedSourcePackageId,
                subSoundName = subSoundName,
                subSoundIndex = subSoundIndex,
                files = new List<SoundpackAudioFile>(files.Count),
                volumeRange = volumeRange?.Clone(),
                volumeRangeMalformed = volumeRangeMalformed,
                extensionData = extensionData?.DeepClone()
            };
            foreach (var file in files)
            {
                clone.files.Add(file.Clone());
            }
            return clone;
        }

        public string TargetKey()
        {
            return (soundDef ?? string.Empty) + "" + (subSoundName ?? string.Empty) + "" + subSoundIndex;
        }
    }

    public sealed class SoundpackVolumeRange
    {
        public float min;
        public float max;
        public JsonValue extensionData;

        public SoundpackVolumeRange Clone() => new SoundpackVolumeRange
        {
            min = min, max = max, extensionData = extensionData?.DeepClone()
        };
    }

    public sealed class SoundpackAudioFile
    {
        public string path;
        public float volume = 1f;
        public bool volumeMalformed;

        public JsonValue extensionData;

        public SoundpackAudioFile Clone()
        {
            return new SoundpackAudioFile
            {
                path = path,
                volume = volume,
                volumeMalformed = volumeMalformed,
                extensionData = extensionData?.DeepClone()
            };
        }
    }
}
