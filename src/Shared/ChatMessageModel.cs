namespace Shared.Models
{
    public class ChatMessageModel
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsUser { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int TTL { get; set; } = 0;
    }
} 