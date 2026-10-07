using System;
using System.IO;
using System.Text;

namespace InfrastructureLayer.Storage
{
    /// <summary>
    /// Filesystem-safe name sanitization shared by tenant-folder naming (point 2 of the
    /// "Print Images &amp; Product Print Configuration" revision) and original-filename
    /// preservation (point 3). Neither <c>Tenant.Username</c> (up to 100 chars, no server-side
    /// character validation) nor a client-supplied file name is guaranteed safe for use as a path
    /// segment — this class is the one place that makes them so, cross-platform, without guessing
    /// the deployment OS: it sanitizes against the stricter Windows-invalid character set even if
    /// the actual deployment is Linux, since a name that is safe on Windows is also safe on Linux,
    /// but not the reverse.
    /// </summary>
    internal static class FileSystemNameSanitizer
    {
        private static readonly char[] InvalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

        // Windows-reserved device names — invalid as a file or folder name regardless of
        // extension or case.
        private static readonly string[] ReservedNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        /// <summary>
        /// Sanitizes <paramref name="username"/> into a folder name. Falls back to
        /// <c>tenant-{tenantId}</c> — never empty — if sanitization strips the username down to
        /// nothing (e.g. a username built entirely from invalid characters).
        /// </summary>
        public static string SanitizeTenantFolder(string username, long tenantId)
        {
            string sanitized = Sanitize(username, maxLength: 80);
            return sanitized.Length == 0 ? $"tenant-{tenantId}" : sanitized;
        }

        /// <summary>
        /// Sanitizes <paramref name="originalFileName"/>'s base name, preserving its extension.
        /// Returns <c>null</c> (never a fallback name) when sanitization strips the base name
        /// down to nothing — a client-supplied file name that can't be made safe should be
        /// rejected with a validation error, not silently renamed to something the client didn't
        /// choose.
        /// </summary>
        public static string? SanitizeFileName(string originalFileName)
        {
            string extension = Path.GetExtension(originalFileName);
            string baseName = Path.GetFileNameWithoutExtension(originalFileName);

            string sanitizedBase = Sanitize(baseName, maxLength: Math.Max(1, 200 - extension.Length));
            return sanitizedBase.Length == 0 ? null : sanitizedBase + extension;
        }

        private static string Sanitize(string value, int maxLength)
        {
            var builder = new StringBuilder(value.Length);

            foreach (char c in value)
            {
                if (char.IsControl(c) || Array.IndexOf(InvalidChars, c) >= 0)
                {
                    builder.Append('_');
                }
                else
                {
                    builder.Append(c);
                }
            }

            // Windows disallows trailing dots and spaces; leading/trailing whitespace is just
            // untidy either way.
            string result = builder.ToString().Trim().TrimEnd('.', ' ');

            if (result.Length > maxLength)
            {
                result = result[..maxLength].TrimEnd('.', ' ');
            }

            if (Array.Exists(ReservedNames, r => string.Equals(r, result, StringComparison.OrdinalIgnoreCase)))
            {
                result = "_" + result;
            }

            return result;
        }
    }
}
