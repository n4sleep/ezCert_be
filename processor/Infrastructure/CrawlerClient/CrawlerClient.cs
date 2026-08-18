using System.Net.Http.Headers;

namespace EzCert.Processor.Infrastructure.CrawlerClient;

// HTTP client for the separately-deployed crawler service (WS-3B).
// Bearer auth matches CRAWLER_SECRET on the crawler; all target URLs are
// validated there (scheme/loopback/private/link-local + DNS checks).
public sealed class CrawlerClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CrawlerClient> _log;

    public CrawlerClient(HttpClient http, IConfiguration config, ILogger<CrawlerClient> log)
    {
        _http = http;
        _log = log;
        _http.Timeout = TimeSpan.FromSeconds(60); // a hung crawler/search must fail the job, not hang it forever
        var baseUrl = config["Crawler:Url"];
        var secret = config["Crawler:Secret"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrWhiteSpace(secret))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
    }

    public bool Configured => _http.BaseAddress is not null && _http.DefaultRequestHeaders.Authorization is not null;

    public record CrawledDocument(string CanonicalUrl, string Title, string Markdown, string ContentHash, string FetchedAt);

    public async Task<List<CrawledDocument>> SearchAsync(string topic, int limit, CancellationToken ct = default)
    {
        return await PostAsync<List<CrawledDocument>>("search", new { topic, limit }, ct)
            ?? throw new InvalidOperationException("crawler search returned no response");
    }

    public async Task<List<CrawledDocument>> CrawlAsync(string url, int limit, CancellationToken ct = default)
    {
        return await PostAsync<List<CrawledDocument>>("crawl", new { url, limit }, ct)
            ?? throw new InvalidOperationException("crawler crawl returned no response");
    }

    private async Task<T?> PostAsync<T>(string path, object body, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync(path, body, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            _log.LogWarning("Crawler {Path} -> {Status}: {Err}", path, (int)resp.StatusCode, err);
            return default;
        }
        return await resp.Content.ReadFromJsonAsync<T>(ct);
    }
}
