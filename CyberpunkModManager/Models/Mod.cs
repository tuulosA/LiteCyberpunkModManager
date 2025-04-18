using System.Reflection;

namespace CyberpunkModManager.Models
{
    public class Mod
    {
        public int ModId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<ModFile> Files { get; set; } = new();
    }
}
