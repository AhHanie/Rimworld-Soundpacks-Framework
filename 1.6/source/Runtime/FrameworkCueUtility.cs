using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace Soundpacks_Framework.Runtime
{
    public static class FrameworkCueUtility
    {
        private static readonly HashSet<string> WarnedCues = new HashSet<string>();

        public static void Reset()
        {
            WarnedCues.Clear();
        }

        public static bool TryGetPlayableDuration(SoundDef soundDef, string cueName, out float duration)
        {
            duration = 0f;

            if (soundDef == null)
            {
                WarnOnce(cueName, "def is null", isIntentional: false);
                return false;
            }

            if (soundDef.sustain)
            {
                WarnOnce(cueName, "def is a sustainer, not a one-shot", isIntentional: false);
                return false;
            }

            if (soundDef.subSounds == null || soundDef.subSounds.Count == 0)
            {
                WarnOnce(cueName, "def has no sub-sounds", isIntentional: false);
                return false;
            }

            if (soundDef.context != SoundContext.Any)
            {
                WarnOnce(cueName, "def context is not Any", isIntentional: false);
                return false;
            }

            bool hasIntentionalGrains = false;
            foreach (var subSound in soundDef.subSounds)
            {
                if (!subSound.onCamera)
                {
                    WarnOnce(cueName, "sub-sound is not on-camera", isIntentional: false);
                    return false;
                }

                if (subSound.grains == null || subSound.grains.Count == 0)
                {
                    WarnOnce(cueName, "sub-sound has no grains", isIntentional: false);
                    return false;
                }

                bool isSilenceBaseline = subSound.grains.Count == 1 && subSound.grains[0] is AudioGrain_Silence;
                if (!isSilenceBaseline)
                {
                    hasIntentionalGrains = true;
                }
            }

            if (!hasIntentionalGrains)
            {
                WarnOnce(cueName, "cue is unmapped; silence baseline only", isIntentional: true);
                return false;
            }

            float max = soundDef.Duration.max;
            if (float.IsNaN(max) || float.IsInfinity(max) || max <= 0f)
            {
                WarnOnce(cueName, "mapped grains resolved to an invalid duration (" + max + ")", isIntentional: false);
                return false;
            }

            duration = max;
            return true;
        }

        private static void WarnOnce(string cueName, string reason, bool isIntentional)
        {
            if (!WarnedCues.Add(cueName))
            {
                return;
            }

            string message = "Skipping framework cue '" + cueName + "': " + reason + ".";
            if (isIntentional)
            {
                Logger.Info(message);
            }
            else
            {
                Logger.Warning(message);
            }
        }
    }
}
