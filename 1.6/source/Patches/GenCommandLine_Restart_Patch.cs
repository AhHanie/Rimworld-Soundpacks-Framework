using HarmonyLib;
using Soundpacks_Framework.Runtime;
using Verse;

namespace Soundpacks_Framework.Patches
{
    [HarmonyPatch(typeof(GenCommandLine), nameof(GenCommandLine.Restart))]
    public static class GenCommandLine_Restart_Patch
    {
        public static void Prefix()
        {
            QuitCuePlayer.ArmPassThroughOnce();
        }
    }
}
