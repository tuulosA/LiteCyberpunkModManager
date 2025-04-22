using System;
using System.IO;
using System.Text.RegularExpressions;

namespace LiteCyberpunkModManager.Helpers
{
    public static class FileNameCleaner
    {
        public static string ExtractCleanName(string original)
        {
            if (string.IsNullOrWhiteSpace(original))
                return "Unknown";

            // If there's a pattern like -12345-1-2-123456789 at the end, strip it
            // Match last dash and check if the suffix is numbers and dashes
            var parts = original.Split('-');

            // Pattern: ..., modid, x, y, timestamp => at least 4 trailing numeric parts
            int numericGroupCount = 0;
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (int.TryParse(parts[i], out _))
                    numericGroupCount++;
                else
                    break;
            }

            // If we found at least 3 numeric groups, assume it's metadata and strip them
            if (numericGroupCount >= 3)
            {
                int cutIndex = parts.Length - numericGroupCount;
                return string.Join("-", parts.Take(cutIndex));
            }

            // Otherwise, assume it's already clean
            return original;
        }

    }
}
