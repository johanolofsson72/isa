using System.ClientModel.Primitives;

namespace Agent.Services.Resilience;

/// <summary>
/// PipelinePolicy that attaches Cloudflare Access service-token headers and a custom User-Agent
/// on every OpenAI SDK request. Used instead of HttpClient.DefaultRequestHeaders because
/// System.ClientModel pipeline can bypass HttpClient header merging, and the SDK's default
/// User-Agent triggers Cloudflare WAF (returns "Your request was blocked").
/// </summary>
public class CloudflareAccessHeadersPolicy(string clientId, string clientSecret, string userAgent) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ApplyHeaders(message);
        if (currentIndex < pipeline.Count - 1)
        {
            pipeline[currentIndex + 1].Process(message, pipeline, currentIndex + 1);
        }
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        ApplyHeaders(message);
        if (currentIndex < pipeline.Count - 1)
        {
            await pipeline[currentIndex + 1].ProcessAsync(message, pipeline, currentIndex + 1);
        }
    }

    private void ApplyHeaders(PipelineMessage message)
    {
        message.Request.Headers.Set("CF-Access-Client-Id", clientId);
        message.Request.Headers.Set("CF-Access-Client-Secret", clientSecret);
        message.Request.Headers.Set("User-Agent", userAgent);
    }
}
