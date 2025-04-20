namespace LiteCyberpunkModManager.Models
{
    public class ModFile
    {
        public int FileId { get; set; }
        public string FileName { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public DateTime UploadedTimestamp { get; set; }
        public string Description { get; set; } = "";
    }
}
