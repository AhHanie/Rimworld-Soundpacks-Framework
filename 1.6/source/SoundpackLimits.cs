namespace Soundpacks_Framework
{
    public static class SoundpackLimits
    {
        public const int MaxArchiveEntries = 2000;
        public const long MaxTotalUncompressedBytes = 512L * 1024 * 1024;
        public const long MaxTotalCompressedBytes = 128L * 1024 * 1024;
        public const long MaxPerAudioFileBytes = 64L * 1024 * 1024;
        public const long MaxManifestBytes = 100L * 1024 * 1024;
        public const int MaxCompressionRatio = 100;

        public const double MaxClipDurationSeconds = 120.0;
        public const long MaxDecodedPcmBytesPerClip = 64L * 1024 * 1024;

        public const int MinSampleRateHz = 8000;
        public const int MaxSampleRateHz = 48000;
        public const int MaxChannels = 2;

        public const float MinSubSoundVolumeRangePercent = 0f;
        public const float MaxSubSoundVolumeRangePercent = 100f;
    }
}
