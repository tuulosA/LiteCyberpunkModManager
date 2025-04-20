namespace LiteCyberpunkModManager.Models
{
    public class InstalledModInfo
    {
        public int ModId { get; set; }
        public string ModName { get; set; } = "";
        public int FileId { get; set; }
        public string FileName { get; set; } = "";
        public DateTime UploadedTimestamp { get; set; }
    }
}
