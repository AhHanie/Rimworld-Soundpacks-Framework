using HarmonyLib;
using Soundpacks_Framework.Runtime;
using Verse;

namespace Soundpacks_Framework.Patches
{
    [HarmonyPatch(typeof(Root), nameof(Root.Shutdown))]
    public static class Root_Shutdown_Patch
    {
        public static bool Prefix()
        {
            return QuitCuePlayer.TryBeginQuit();
        }
    }
}
