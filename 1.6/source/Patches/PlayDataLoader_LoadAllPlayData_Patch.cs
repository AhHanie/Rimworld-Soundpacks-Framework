using HarmonyLib;
using Soundpacks_Framework.Runtime;
using Verse;

namespace Soundpacks_Framework.Patches
{
    [HarmonyPatch(typeof(PlayDataLoader), nameof(PlayDataLoader.LoadAllPlayData))]
    public static class PlayDataLoader_LoadAllPlayData_Patch
    {
        public static void Postfix()
        {
            SoundpackBootstrap.ScheduleAfterPlayDataLoaded();
        }
    }
}
