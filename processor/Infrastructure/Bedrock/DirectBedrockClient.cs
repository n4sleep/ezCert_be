using System.Text;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;

namespace EzCert.Processor.Infrastructure.Bedrock;

// Direct Bedrock access (local dev only — requires SSO/AWS credentials in the
// environment). Hosted mode uses GatewayBedrockClient instead (AD-14).
public class DirectBedrockClient : IBedrockClient
{
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly string _embedModel;
    private readonly string _genModel;

    public DirectBedrockClient(string embedModel, string genModel, string region)
    {
        _embedModel = embedModel;
        _genModel = genModel;
        _client = new AmazonBedrockRuntimeClient(Amazon.RegionEndpoint.GetBySystemName(region));
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var resp = await _client.InvokeModelAsync(new InvokeModelRequest
        {
            ModelId = _embedModel,
            Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { inputText = text }))),
        }, ct);

        using var doc = JsonDocument.Parse(resp.Body);
        var embedding = doc.RootElement.GetProperty("embedding");
        var values = new List<float>();
        foreach (var e in embedding.EnumerateArray())
            values.Add(e.GetSingle());
        return values.ToArray();
    }

    public async Task<string> GenerateAsync(string prompt, string? system, int maxTokens, float temperature, CancellationToken ct = default)
    {
        var systemBlocks = system is null
            ? null
            : new List<SystemContentBlock> { new SystemContentBlock { Text = system } };

        var resp = await _client.ConverseAsync(new ConverseRequest
        {
            ModelId = _genModel,
            Messages = new List<Message>
            {
                new Message { Role = ConversationRole.User, Content = new List<ContentBlock> { new ContentBlock { Text = prompt } } },
            },
            System = systemBlocks,
            InferenceConfig = new InferenceConfiguration { MaxTokens = maxTokens, Temperature = temperature },
        }, ct);

        return string.Concat(resp.Output?.Message?.Content?.Select(c => c.Text ?? "") ?? Enumerable.Empty<string>());
    }

    public async Task<string> ExplainAsync(string question, string correctAnswer, string? userAnswer, string? slug, CancellationToken ct = default)
    {
        var system = "You are a friendly cloud certification tutor. Explain concisely (2-4 sentences) and be encouraging and factual.";
        var wrong = string.IsNullOrWhiteSpace(userAnswer)
            ? ""
            : $"\nThe learner answered: {userAnswer}. If that is incorrect, gently explain why.";
        var prompt = $"Question: {question}\nCorrect answer: {correctAnswer}{wrong}\n\nExplain why the correct answer is right.";
        return await GenerateAsync(prompt, system, 400, 0.3f, ct);
    }
}
