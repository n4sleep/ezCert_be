using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EzCert.Processor.Infrastructure.Bedrock;

// Hosted Bedrock access via the rich-sandbox gateway (AD-14). The processor
// never holds AWS keys; it forwards to the gateway with a shared secret.
public class GatewayBedrockClient : IBedrockClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _secret;

    public GatewayBedrockClient(string baseUrl, string secret)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _secret = secret;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var resp = await PostAsync("/api/bedrock/embed", new { text }, ct);
        using var doc = JsonDocument.Parse(resp);
        var embedding = doc.RootElement.GetProperty("embedding");
        var values = new List<float>();
        foreach (var e in embedding.EnumerateArray())
            values.Add(e.GetSingle());
        return values.ToArray();
    }

    public async Task<string> GenerateAsync(string prompt, string? system, int maxTokens, float temperature, CancellationToken ct = default)
    {
        var resp = await PostAsync("/api/bedrock/generate", new { prompt, system, maxTokens, temperature }, ct);
        return ReadText(resp);
    }

    public async Task<string> ExplainAsync(string question, string correctAnswer, string? userAnswer, string? slug, CancellationToken ct = default)
    {
        var resp = await PostAsync("/api/bedrock/explain", new { question, correctAnswer, userAnswer, slug }, ct);
        return ReadText(resp);
    }

    private async Task<string> PostAsync(string path, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, Json);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{path}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _secret);

        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Bedrock gateway {path} failed ({resp.StatusCode}): {text}");
        return text;
    }

    private static string ReadText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("text").GetString() ?? "";
    }
}
