using System.IO;

namespace HelixModManager.Helpers
{
    public static class PathUtils
    {
        public static string SanitizeModName(string name)
        {
            return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        }
    }
}

