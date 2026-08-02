using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundpacks_Framework.Audio
{
    public static class AudioDecodeService
    {
        private static readonly string[] SupportedExtensions = { ".mp3", ".wav", ".ogg" };

        public static bool IsSupportedExtension(string path)
        {
            string ext = Path.GetExtension(path);
            foreach (var supported in SupportedExtensions)
            {
                if (string.Equals(ext, supported, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string ComputeFileHashHex(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static AudioProbeInfo ProbeHeader(string path)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                switch (ext)
                {
                    case ".mp3":
                        using (var reader = new Mp3FileReader(path))
                        {
                            return ProbeFromWaveStream(reader);
                        }
                    case ".wav":
                        using (var reader = new WaveFileReader(path))
                        {
                            return ProbeFromWaveStream(reader);
                        }
                    case ".ogg":
                        using (var reader = new NVorbis.VorbisReader(path))
                        {
                            return new AudioProbeInfo
                            {
                                Success = true,
                                SampleRate = reader.SampleRate,
                                Channels = reader.Channels,
                                DurationSeconds = reader.TotalTime.TotalSeconds
                            };
                        }
                    default:
                        return new AudioProbeInfo { Success = false, Error = "Unsupported audio extension '" + ext + "'." };
                }
            }
            catch (Exception ex)
            {
                return new AudioProbeInfo { Success = false, Error = "Corrupt or unreadable audio header: " + ex.Message };
            }
        }

        private static AudioProbeInfo ProbeFromWaveStream(WaveStream reader)
        {
            return new AudioProbeInfo
            {
                Success = true,
                SampleRate = reader.WaveFormat.SampleRate,
                Channels = reader.WaveFormat.Channels,
                DurationSeconds = reader.TotalTime.TotalSeconds
            };
        }

        public static AudioDecodeResult Decode(string path)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                switch (ext)
                {
                    case ".mp3": return DecodeWithNAudio(path, () => new Mp3FileReader(path));
                    case ".wav": return DecodeWithNAudio(path, () => OpenWav(path));
                    case ".ogg": return DecodeOgg(path);
                    default: return Fail("Unsupported audio extension '" + ext + "'.");
                }
            }
            catch (Exception ex)
            {
                return Fail("Failed to decode '" + path + "': " + ex.Message);
            }
        }

        private static WaveStream OpenWav(string path)
        {
            WaveStream reader = new WaveFileReader(path);
            if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm && reader.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                reader = WaveFormatConversionStream.CreatePcmStream(reader);
                reader = new BlockAlignReductionStream(reader);
            }
            return reader;
        }

        private static AudioDecodeResult DecodeWithNAudio(string path, Func<WaveStream> openReader)
        {
            using (WaveStream reader = openReader())
            {
                var sampleProvider = new SampleChannel(reader);
                int sampleRate = sampleProvider.WaveFormat.SampleRate;
                int channels = sampleProvider.WaveFormat.Channels;

                if (!ValidateFormat(sampleRate, channels, out string formatError))
                {
                    return Fail(formatError);
                }

                var buffer = new float[8192];
                var samples = new List<float>();
                int read;
                while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (!AppendValidated(samples, buffer, read, out string sampleError))
                    {
                        return Fail(sampleError);
                    }
                }

                return BuildResult(path, samples, sampleRate, channels);
            }
        }

        private static AudioDecodeResult DecodeOgg(string path)
        {
            using (var reader = new NVorbis.VorbisReader(path))
            {
                int sampleRate = reader.SampleRate;
                int channels = reader.Channels;

                if (!ValidateFormat(sampleRate, channels, out string formatError))
                {
                    return Fail(formatError);
                }

                var buffer = new float[8192];
                var samples = new List<float>();
                int read;
                while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
                {
                    if (!AppendValidated(samples, buffer, read, out string sampleError))
                    {
                        return Fail(sampleError);
                    }
                }

                return BuildResult(path, samples, sampleRate, channels);
            }
        }

        private static bool ValidateFormat(int sampleRate, int channels, out string error)
        {
            if (channels < 1 || channels > SoundpackLimits.MaxChannels)
            {
                error = "Unsupported channel count " + channels + " (must be mono or stereo).";
                return false;
            }
            if (sampleRate < SoundpackLimits.MinSampleRateHz || sampleRate > SoundpackLimits.MaxSampleRateHz)
            {
                error = "Sample rate " + sampleRate + " Hz is outside the supported range (" +
                        SoundpackLimits.MinSampleRateHz + "-" + SoundpackLimits.MaxSampleRateHz + " Hz).";
                return false;
            }
            error = null;
            return true;
        }

        private static bool AppendValidated(List<float> samples, float[] buffer, int count, out string error)
        {
            for (int i = 0; i < count; i++)
            {
                float sample = buffer[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample))
                {
                    error = "Decoded audio contains a NaN/Infinity sample; the source file is corrupt.";
                    return false;
                }
            }
            samples.AddRange(new ArraySegment<float>(buffer, 0, count));
            if ((long)samples.Count * sizeof(float) > SoundpackLimits.MaxDecodedPcmBytesPerClip)
            {
                error = "Decoded PCM exceeds the " + (SoundpackLimits.MaxDecodedPcmBytesPerClip / (1024 * 1024)) + " MiB per-clip limit.";
                return false;
            }
            error = null;
            return true;
        }

        private static AudioDecodeResult BuildResult(string path, List<float> samples, int sampleRate, int channels)
        {
            if (samples.Count == 0)
            {
                return Fail("Decoded audio has zero frames.");
            }

            int frameCount = samples.Count / channels;
            double durationSeconds = (double)frameCount / sampleRate;
            if (durationSeconds > SoundpackLimits.MaxClipDurationSeconds)
            {
                return Fail("Clip duration " + durationSeconds.ToString("0.0") + "s exceeds the " +
                             SoundpackLimits.MaxClipDurationSeconds + "s per-clip limit.");
            }

            return new AudioDecodeResult
            {
                Success = true,
                Audio = new DecodedAudio
                {
                    SourcePath = path,
                    ContentHashHex = ComputeFileHashHex(path),
                    SampleRate = sampleRate,
                    Channels = channels,
                    FrameCount = frameCount,
                    Samples = samples.ToArray()
                }
            };
        }

        private static AudioDecodeResult Fail(string error) => new AudioDecodeResult { Success = false, Error = error };
    }
}
