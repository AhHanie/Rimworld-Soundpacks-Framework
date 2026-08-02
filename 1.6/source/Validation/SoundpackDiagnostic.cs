namespace Soundpacks_Framework.Validation
{
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class SoundpackDiagnostic
    {
        public DiagnosticSeverity Severity;
        public string Code;
        public string PackId;
        public string Location;
        public string Message;
        public string Remedy;

        public bool IsError => Severity == DiagnosticSeverity.Error;

        public static SoundpackDiagnostic Error(string code, string packId, string location, string message, string remedy) =>
            new SoundpackDiagnostic { Severity = DiagnosticSeverity.Error, Code = code, PackId = packId, Location = location, Message = message, Remedy = remedy };

        public static SoundpackDiagnostic Warn(string code, string packId, string location, string message, string remedy) =>
            new SoundpackDiagnostic { Severity = DiagnosticSeverity.Warning, Code = code, PackId = packId, Location = location, Message = message, Remedy = remedy };

        public static SoundpackDiagnostic Info(string code, string packId, string location, string message, string remedy = null) =>
            new SoundpackDiagnostic { Severity = DiagnosticSeverity.Info, Code = code, PackId = packId, Location = location, Message = message, Remedy = remedy };

        public override string ToString()
        {
            string remedyPart = string.IsNullOrEmpty(Remedy) ? "" : $" Remedy: {Remedy}";
            return $"[{Severity}] {Code} pack='{PackId}' at='{Location}': {Message}.{remedyPart}";
        }
    }
}
