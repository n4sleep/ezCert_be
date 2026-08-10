using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Options;

namespace EzCert.Api.Rag;

public interface IBedrockClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<string> GenerateAsync(string prompt, string? system = null, int maxTokens = 512, float temperature = 0.2f, CancellationToken ct = default);
}

// Thin wrapper over AWS Bedrock Runtime.
// - Embeddings: Titan Text Embeddings v2 via InvokeModel (raw JSON body).
// - Generation: Nova / Claude via the model-agnostic Converse API.
// Credentials are resolved from the default AWS chain (env vars); nothing is read from config.
public sealed class BedrockClient : IBedrockClient
{
    private readonly AmazonBedrockRuntimeClient _client;
    private readonly RagOptions _opt;
    private readonly ILogger<BedrockClient> _log;

    public BedrockClient(IOptions<RagOptions> opt, ILogger<BedrockClient> log)
    {
        _opt = opt.Value;
        _log = log;
        _client = new AmazonBedrockRuntimeClient(RegionEndpoint.GetBySystemName(_opt.Region));
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            inputText = text,
            dimensions = _opt.EmbeddingDim,
            normalize = true
        });

        var req = new InvokeModelRequest
        {
            ModelId = _opt.EmbeddingModel,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(payload))
        };

        var resp = await _client.InvokeModelAsync(req, ct);
        using var doc = await JsonDocument.ParseAsync(resp.Body, cancellationToken: ct);
        var arr = doc.RootElement.GetProperty("embedding");
        var vec = new float[arr.GetArrayLength()];
        var i = 0;
        foreach (var v in arr.EnumerateArray())
            vec[i++] = v.GetSingle();
        return vec;
    }

    public async Task<string> GenerateAsync(string prompt, string? system = null, int maxTokens = 512, float temperature = 0.2f, CancellationToken ct = default)
    {
        var req = new ConverseRequest
        {
            ModelId = _opt.GenerationModel,
            Messages = new List<Message>
            {
                new()
                {
                    Role = ConversationRole.User,
                    Content = new List<ContentBlock> { new() { Text = prompt } }
                }
            },
            InferenceConfig = new InferenceConfiguration
            {
                MaxTokens = maxTokens,
                Temperature = temperature
            }
        };

        if (!string.IsNullOrWhiteSpace(system))
            req.System = new List<SystemContentBlock> { new() { Text = system } };

        var resp = await _client.ConverseAsync(req, ct);
        var blocks = resp.Output?.Message?.Content;
        if (blocks is null || blocks.Count == 0)
            return string.Empty;
        return string.Concat(blocks.Where(b => b.Text is not null).Select(b => b.Text));
    }
}
