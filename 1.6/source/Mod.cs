using HarmonyLib;
using Soundpacks_Framework.Runtime;
using Soundpacks_Framework.Settings;
using Soundpacks_Framework.Storage;
using UnityEngine;
using Verse;

namespace Soundpacks_Framework
{
    public class Mod : Verse.Mod
    {
        public Mod(ModContentPack content) : base(content)
        {
            SoundpackRepository.RecoverOnStartup();
            SoundpackSettingsController.Load();
            new Harmony("sk.soundpacks").PatchAll();
            SoundpackBootstrap.ScheduleAfterPlayDataLoaded();
        }

        public override string SettingsCategory()
        {
            return "SPF.SettingsTitle".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ModSettingsWindow.Draw(inRect);
        }
    }
}
