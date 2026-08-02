using System.Collections.Generic;
using UnityEngine;

namespace Soundpacks_Framework.Audio
{
    public static class RuntimeClipFactory
    {
        private static readonly HashSet<AudioClip> OwnedClips = new HashSet<AudioClip>();

        public static int OwnedCount => OwnedClips.Count;

        public static float[] ApplyGain(float[] samples, float gain)
        {
            if (gain == 1f)
            {
                return samples;
            }

            var scaled = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                scaled[i] = Mathf.Clamp(samples[i] * gain, -1f, 1f);
            }
            return scaled;
        }

        public static AudioClip CreateClip(DecodedAudio audio, string clipName, float gain)
        {
            var clip = AudioClip.Create(clipName, audio.FrameCount, audio.Channels, audio.SampleRate, stream: false);
            clip.SetData(ApplyGain(audio.Samples, gain), 0);
            OwnedClips.Add(clip);
            return clip;
        }

        public static void DestroyAllOwned()
        {
            foreach (var clip in OwnedClips)
            {
                if (clip != null)
                {
                    Object.Destroy(clip);
                }
            }
            OwnedClips.Clear();
        }
    }
}
