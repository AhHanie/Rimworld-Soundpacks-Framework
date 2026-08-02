using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Soundpacks_Framework.Runtime
{
    public static class QuitCuePlayer
    {
        private const float CompletionMarginSeconds = 0.25f;

        private static bool _pending;
        private static bool _passThroughOnce;
        private static float _deadlineRealtime;

        public static void Reset()
        {
            _pending = false;
            _passThroughOnce = false;
        }

        public static void ArmPassThroughOnce()
        {
            _passThroughOnce = true;
        }

        public static bool TryBeginQuit()
        {
            if (_passThroughOnce)
            {
                _passThroughOnce = false;
                return true;
            }

            if (_pending)
            {
                return false;
            }

            if (!SoundpackRuntimeController.IsActive || Find.SoundRoot == null)
            {
                return true;
            }

            SoundDef soundDef = SoundpacksFrameworkSoundDefOf.SoundpacksFramework_Quit;
            if (!FrameworkCueUtility.TryGetPlayableDuration(soundDef, "Quit", out float duration))
            {
                return true;
            }

            if (!SoundSlotManager.CanPlayNow(soundDef.slot))
            {
                return true;
            }

            _pending = true;
            try
            {
                soundDef.PlayOneShotOnCamera();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Failed to play framework quit cue");
                _pending = false;
                return true;
            }

            _deadlineRealtime = Time.realtimeSinceStartup + duration + CompletionMarginSeconds;
            return false;
        }

        public static void TryCompleteQuit()
        {
            if (!_pending || Time.realtimeSinceStartup < _deadlineRealtime)
            {
                return;
            }

            _pending = false;
            _passThroughOnce = true;
            Root.Shutdown();
        }
    }
}
