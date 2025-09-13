using System;

namespace LiteCyberpunkModManager.Helpers
{
    public static class FileNameHelper
    {
        // trailing extensions we consider "archive-y" in the Nexus "name" field
        private static readonly string[] ArchiveExts = new[]
        {
            ".zip", ".rar", ".7z"
        };

        /// <summary>
        /// Takes Nexus "name" (human display name) and ensures the final filename
        /// ends with exactly one ".zip". If the name itself ends with an archive
        /// extension (even multiple), they’re removed before adding ".zip".
        /// </summary>
        public static string NormalizeDisplayFileName(string apiName)
        {
            if (string.IsNullOrWhiteSpace(apiName))
                return "file.zip";

            var name = apiName.Trim();

            // Strip *all* trailing archive extensions (handles weird cases like "foo.zip.7z")
            bool stripped;
            do
            {
                stripped = false;
                foreach (var ext in ArchiveExts)
                {
                    if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        name = name.Substring(0, name.Length - ext.Length);
                        stripped = true;
                        break;
                    }
                }
            } while (stripped);

            // Guard against leftover trailing dots/spaces
            name = name.TrimEnd('.', ' ');

            if (string.IsNullOrEmpty(name))
                name = "file";

            return name + ".zip";
        }
    }
}
