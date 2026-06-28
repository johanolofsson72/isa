namespace Agent.Models
{
    public class McpClientStats
    {
        public int TotalClients { get; set; }
        public int ActiveClients { get; set; }
    }

    public class McpClientInfo
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string Origin { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public long RequestCount { get; set; }
    }
}
