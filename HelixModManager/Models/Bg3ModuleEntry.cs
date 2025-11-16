using System;

namespace HelixModManager.Models
{
    public class Bg3ModuleEntry
    {
        public string UUID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public long Version64 { get; set; }
    }
}


