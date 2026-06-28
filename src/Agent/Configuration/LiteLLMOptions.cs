namespace Agent.Configuration;

public class LiteLLMOptions
{
    public const string SectionName = "LiteLLM";

    public string Endpoint { get; set; } = "https://llm.example.com/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string ChatModel { get; set; } = "chat";
    public string EmbedModel { get; set; } = "embed";
    public int EmbeddingDimensions { get; set; } = 768;
    public int TimeoutMinutes { get; set; } = 5;
    public string UserAgent { get; set; } = "accountingagent/1.0";
}
