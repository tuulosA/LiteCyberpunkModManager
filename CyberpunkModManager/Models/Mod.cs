using System.Reflection;

namespace CyberpunkModManager.Models
{
    public class Mod
    {
        public int ModId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public List<ModFile> Files { get; set; } = new();
    }
}
