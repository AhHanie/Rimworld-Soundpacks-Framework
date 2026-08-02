using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Soundpacks_Framework.Storage
{
    public static class SoundpackPathPolicy
    {
        public const string AudioFolderPrefix = "Audio/";
        public const string ManifestFileName = "soundpack.json";

        private static readonly Regex PackIdPattern = new Regex(
            "^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.Compiled);

        public static bool IsValidPackId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 200)
            {
                return false;
            }
            return PackIdPattern.IsMatch(id);
        }

        public static string NewGuidPackId()
        {
            return "user." + Guid.NewGuid().ToString("N");
        }

        public static bool TryCanonicalizeArchiveEntryPath(string rawEntryName, out string canonical, out string error)
        {
            canonical = null;
            error = null;

            if (string.IsNullOrEmpty(rawEntryName))
            {
                error = "Empty archive entry name.";
                return false;
            }
            if (rawEntryName.IndexOf('\0') >= 0)
            {
                error = "Archive entry name contains a NUL byte.";
                return false;
            }

            string normalized = rawEntryName.Replace('\\', '/');

            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                error = "Archive entry name must not be absolute (leading '/').";
                return false;
            }
            if (normalized.Length >= 2 && normalized[1] == ':')
            {
                error = "Archive entry name must not contain a drive prefix.";
                return false;
            }

            string[] segments = normalized.Split('/');
            foreach (var segment in segments)
            {
                if (segment.Length == 0)
                {
                    error = "Archive entry name contains an empty path segment.";
                    return false;
                }
                if (segment == "." || segment == "..")
                {
                    error = "Archive entry name contains a traversal segment ('.' or '..').";
                    return false;
                }
            }

            canonical = normalized;
            return true;
        }

        public static bool IsUnderAudioFolder(string canonicalPath)
        {
            return canonicalPath != null &&
                   canonicalPath.StartsWith(AudioFolderPrefix, StringComparison.Ordinal);
        }

        public static string CaseInsensitiveKey(string canonicalPath)
        {
            return canonicalPath?.ToUpperInvariant();
        }

        public static bool IsContainedWithin(string baseDirFullPath, string candidateFullPath)
        {
            string basePath = Path.GetFullPath(baseDirFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(candidateFullPath);
            return candidate.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        }

        public static string CombineContained(string baseDirFullPath, string canonicalRelativePath)
        {
            string combined = Path.GetFullPath(Path.Combine(baseDirFullPath, canonicalRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsContainedWithin(baseDirFullPath, combined))
            {
                throw new InvalidOperationException($"Path '{canonicalRelativePath}' escapes base directory '{baseDirFullPath}'.");
            }
            return combined;
        }
    }
}
