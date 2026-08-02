using HarmonyLib;
using Soundpacks_Framework.Discovery;
using Verse;

namespace Soundpacks_Framework.Patches
{
    [HarmonyPatch(typeof(ModContentPack), nameof(ModContentPack.AnyContentLoaded))]
    public static class ModContentPack_AnyContentLoaded_Patch
    {
        public static void Postfix(ModContentPack __instance, ref bool __result)
        {
            if (__result) return;
            __result = SoundpackContentDetector.HasDiscoverableSoundpack(__instance.foldersToLoadDescendingOrder);
        }
    }
}
