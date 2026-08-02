using HarmonyLib;
using Soundpacks_Framework.Runtime;
using Verse;

namespace Soundpacks_Framework.Patches
{
    [HarmonyPatch(typeof(Root), nameof(Root.Update))]
    public static class Root_Update_Patch
    {
        public static void Postfix()
        {
            StartupCuePlayer.TryPlayWhenReady();
            QuitCuePlayer.TryCompleteQuit();
        }
    }
}
