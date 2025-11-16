namespace HelixModManager.Models
{
    public enum NotificationType
    {
        Info,
        Warning,
        Error
    }

    public class Notification
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public NotificationType Type { get; set; } = NotificationType.Info;

        public Notification() { }

        public Notification(string title, string message, NotificationType type)
        {
            Title = title;
            Message = message;
            Type = type;
        }
    }
}


