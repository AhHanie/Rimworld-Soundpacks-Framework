using System;
using System.IO;
using Soundpacks_Framework.Serialization.Json;
using Soundpacks_Framework.Storage;
using Verse;

namespace Soundpacks_Framework.Settings
{
    public static class SoundpackSettingsController
    {
        private const string SettingsFileName = "Settings.json";

        public static string SettingsPath => Path.Combine(SoundpackRepository.RootPath, SettingsFileName);

        public static void Load()
        {
            SoundpackSettings.ResetToDefaults();

            string path = SettingsPath;
            if (!File.Exists(path))
            {
                SoundpackSettings.bootActivePackId = SoundpackSettings.activePackId;
                return;
            }

            try
            {
                string text = File.ReadAllText(path, System.Text.Encoding.UTF8);
                if (!JsonParser.TryParse(text, out JsonValue root, out string error) || root.Kind != JsonKind.Object)
                {
                    Logger.Error("Corrupt Settings.json (" + (error ?? "not an object") + "); falling back to defaults.");
                    SoundpackSettings.bootActivePackId = SoundpackSettings.activePackId;
                    return;
                }

                SoundpackSettings.activePackId = root.Get("activePackId").AsString("");
                SoundpackSettings.lastPickerDirectory = root.Get("lastPickerDirectory").AsString("");
                SoundpackSettings.managerSearchText = root.Get("managerSearchText").AsString("");
                SoundpackSettings.managerShowReadOnlyPacks = root.Get("managerShowReadOnlyPacks").AsBool(true);
                SoundpackSettings.managerShowDiagnostics = root.Get("managerShowDiagnostics").AsBool(true);
                SoundpackSettings.editorPreviewVolume = (float)root.Get("editorPreviewVolume").AsNumber(1.0);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Failed reading Settings.json; falling back to defaults");
                SoundpackSettings.ResetToDefaults();
            }

            SoundpackSettings.bootActivePackId = SoundpackSettings.activePackId;
        }

        public static void Save()
        {
            var root = JsonValue.NewObject();
            root.Set("activePackId", JsonValue.Of(SoundpackSettings.activePackId ?? ""));
            root.Set("lastPickerDirectory", JsonValue.Of(SoundpackSettings.lastPickerDirectory ?? ""));
            root.Set("managerSearchText", JsonValue.Of(SoundpackSettings.managerSearchText ?? ""));
            root.Set("managerShowReadOnlyPacks", JsonValue.Of(SoundpackSettings.managerShowReadOnlyPacks));
            root.Set("managerShowDiagnostics", JsonValue.Of(SoundpackSettings.managerShowDiagnostics));
            root.Set("editorPreviewVolume", JsonValue.Of(SoundpackSettings.editorPreviewVolume));

            try
            {
                AtomicFileWriter.WriteAllText(SettingsPath, JsonWriter.Write(root, canonical: true, indent: true));
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Failed saving Settings.json");
            }
        }

        public static void ResetToDefaults()
        {
            SoundpackSettings.ResetToDefaults();
            Save();
        }
    }
}
