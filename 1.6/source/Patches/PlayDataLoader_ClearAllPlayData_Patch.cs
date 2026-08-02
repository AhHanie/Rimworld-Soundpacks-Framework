using HarmonyLib;
using Soundpacks_Framework.Runtime;
using Verse;

namespace Soundpacks_Framework.Patches
{
    [HarmonyPatch(typeof(PlayDataLoader), nameof(PlayDataLoader.ClearAllPlayData))]
    public static class PlayDataLoader_ClearAllPlayData_Patch
    {
        public static void Prefix()
        {
            SoundpackBootstrap.TeardownBeforeClear();
        }
    }
}
