using System.Collections.Generic;
using Soundpacks_Framework.Validation;

namespace Soundpacks_Framework.Models
{
    public enum SoundpackSource
    {
        User,
        Mod
    }

    public sealed class SoundpackDescriptor
    {
        public string Id;
        public SoundpackSource Source;
        public string DirectoryPath;
        public bool ReadOnly;

        public string SourceModName;
        public string SourcePackageId;
        public string SourceModVersion;

        public SoundpackManifest Manifest;
        public bool LoadSucceeded;
        public string LoadError;

        public List<SoundpackDiagnostic> Diagnostics = new List<SoundpackDiagnostic>();

        public bool SuppressedByConflict;
        public string ConflictNote;

        public string DisplayName => !string.IsNullOrEmpty(Manifest?.name) ? Manifest.name : Id;

        public bool IsSelectable => LoadSucceeded && !SuppressedByConflict;
    }
}
