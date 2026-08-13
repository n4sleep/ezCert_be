namespace EzCert.Processor.Infrastructure.Bedrock;

// Bedrock abstraction (AD-14): hosted mode routes through the rich-sandbox
// gateway; local dev uses direct Bedrock via SSO credentials. The generation
// pipeline depends only on this interface.
public interface IBedrockClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<string> GenerateAsync(string prompt, string? system, int maxTokens, float temperature, CancellationToken ct = default);
    Task<string> ExplainAsync(string question, string correctAnswer, string? userAnswer, string? slug, CancellationToken ct = default);
}
