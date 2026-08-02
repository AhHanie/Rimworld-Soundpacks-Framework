namespace Soundpacks_Framework.Audio
{
    public sealed class DecodedAudio
    {
        public string SourcePath;
        public string ContentHashHex;
        public int SampleRate;
        public int Channels;
        public int FrameCount;

        public float[] Samples;

        public double DurationSeconds => Channels <= 0 || SampleRate <= 0 ? 0 : (double)FrameCount / SampleRate;
    }

    public sealed class AudioProbeInfo
    {
        public bool Success;
        public string Error;
        public int SampleRate;
        public int Channels;
        public double DurationSeconds;
    }

    public sealed class AudioDecodeResult
    {
        public bool Success;
        public string Error;
        public DecodedAudio Audio;
    }
}
