using Soundpacks_Framework.UI;
using UnityEngine;
using Verse;

namespace Soundpacks_Framework
{
    public static class ModSettingsWindow
    {
        public static void Draw(Rect parent)
        {
            var listing = new Listing_Standard();
            listing.Begin(parent);

            string activeLabel = string.IsNullOrEmpty(SoundpackSettings.activePackId)
                ? "SPF.ActivePackNone".Translate()
                : "SPF.ActivePack".Translate(SoundpackSettings.activePackId);
            listing.Label(activeLabel);

            if (SoundpackSettings.RestartRequired)
            {
                listing.Label("SPF.RestartRequired".Translate());
            }
            else if (!string.IsNullOrEmpty(SoundpackSettings.lastActivationDiagnostic))
            {
                listing.Label(SoundpackSettings.lastActivationDiagnostic);
            }

            listing.Gap(12f);
            if (listing.ButtonText("SPF.OpenManager".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SoundpackManager());
            }

            listing.End();
        }
    }
}
