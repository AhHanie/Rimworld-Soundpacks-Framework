namespace Soundpacks_Framework
{
    public static class SoundpackSettings
    {
        public static string activePackId = "";

        public static string bootActivePackId = "";

        public static bool activationAttempted = false;

        public static string lastActivationDiagnostic = "";

        public static string lastPickerDirectory = "";

        public static string managerSearchText = "";
        public static bool managerShowReadOnlyPacks = true;
        public static bool managerShowDiagnostics = true;

        public static float editorPreviewVolume = 1f;

        public static bool RestartRequired => (activePackId ?? "") != (bootActivePackId ?? "");

        public static void ResetToDefaults()
        {
            activePackId = "";
            bootActivePackId = "";
            activationAttempted = false;
            lastActivationDiagnostic = "";
            lastPickerDirectory = "";
            managerSearchText = "";
            managerShowReadOnlyPacks = true;
            managerShowDiagnostics = true;
            editorPreviewVolume = 1f;
        }
    }
}
