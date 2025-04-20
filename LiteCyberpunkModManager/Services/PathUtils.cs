using System.IO;

namespace LiteCyberpunkModManager.Services
{
    public static class PathUtils
    {
        public static string SanitizeModName(string name)
        {
            return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
