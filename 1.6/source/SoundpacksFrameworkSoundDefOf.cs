using RimWorld;
using Verse;
using Verse.Sound;

namespace Soundpacks_Framework
{
    [DefOf]
    public static class SoundpacksFrameworkSoundDefOf
    {
        public static SoundDef SoundpacksFramework_Startup;
        public static SoundDef SoundpacksFramework_Quit;

        static SoundpacksFrameworkSoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoundpacksFrameworkSoundDefOf));
        }
    }
}
