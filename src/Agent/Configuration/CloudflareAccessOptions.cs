namespace Agent.Configuration;

public class CloudflareAccessOptions
{
    public const string SectionName = "CloudflareAccess";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
