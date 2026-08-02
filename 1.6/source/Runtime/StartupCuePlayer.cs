using System;
using Verse;
using Verse.Sound;

namespace Soundpacks_Framework.Runtime
{
    public static class StartupCuePlayer
    {
        private static bool _handled;

        public static void Reset()
        {
            _handled = false;
        }

        public static void TryPlayWhenReady()
        {
            if (_handled)
            {
                return;
            }

            if (!PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting || Find.SoundRoot == null)
            {
                return;
            }

            _handled = true;

            if (!FrameworkCueUtility.TryGetPlayableDuration(SoundpacksFrameworkSoundDefOf.SoundpacksFramework_Startup, "Startup", out _))
            {
                return;
            }

            try
            {
                SoundpacksFrameworkSoundDefOf.SoundpacksFramework_Startup.PlayOneShotOnCamera();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Failed to play framework startup cue");
            }
        }
    }
}
