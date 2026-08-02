using System;
using Verse;

namespace Soundpacks_Framework.Runtime
{
    public static class SoundpackBootstrap
    {
        private static bool _activationHandled;

        public static void ScheduleAfterPlayDataLoaded()
        {
            Logger.Info("Scheduled activation via LongEventHandler.ExecuteWhenFinished.");

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (_activationHandled)
                {
                    Logger.Info("Activation callback fired again; already handled this load, skipping.");
                    return;
                }
                _activationHandled = true;

                FrameworkCueUtility.Reset();
                StartupCuePlayer.Reset();
                QuitCuePlayer.Reset();

                Logger.Info("Activation callback firing now (after def load/resolution finished).");
                try
                {
                    SoundpackRuntimeController.ActivateSelectedPack();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "Soundpack activation failed; all sound defs are left untouched");
                }
            });
        }

        public static void TeardownBeforeClear()
        {
            try
            {
                SoundpackRuntimeController.DeactivateAndTeardown();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Soundpack teardown failed");
            }
            finally
            {
                _activationHandled = false;
                StartupCuePlayer.Reset();
                QuitCuePlayer.Reset();
            }
        }
    }
}
