using EzCert.Processor.Features.Sources;
using EzCert.Processor.Infrastructure.CrawlerClient;

namespace EzCert.Processor.Features.Generation;

// Topic -> source discovery (WS-3B). Cert topics resolve to the seeded
// official:{CERT} namespaces (no crawling). Arbitrary topics go through
// Firecrawl Search (scraped content in the search response) -> SSRF-safe
// filter on the crawler -> stable source ingestion (contentHash dedupe).
public class DiscoveryService
{
    private readonly CrawlerClient _crawler;
    private readonly SourceService _sources;
    private readonly ILogger<DiscoveryService> _log;

    public DiscoveryService(CrawlerClient crawler, SourceService sources, ILogger<DiscoveryService> log)
    {
        _crawler = crawler;
        _sources = sources;
        _log = log;
    }

    public static bool IsCertTopic(string prompt, string? configCert)
    {
        if (!string.IsNullOrWhiteSpace(configCert)) return true;
        return System.Text.RegularExpressions.Regex.IsMatch(prompt, @"\b(AZ-900|CLF-C02|AI-900|DP-900)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    // Returns the Qdrant namespaces to retrieve evidence from.
    // - cert topic: official:{CERT}
    // - arbitrary topic: source:{id} for each newly ingested or reused doc
    public async Task<List<string>> ResolveNamespacesAsync(string prompt, string? configCert, CancellationToken ct)
    {
        if (IsCertTopic(prompt, configCert))
        {
            var cert = string.IsNullOrWhiteSpace(configCert)
                ? System.Text.RegularExpressions.Regex.Match(prompt, @"\b(AZ-900|CLF-C02|AI-900|DP-900)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value
                : configCert;
            return new List<string> { $"official:{cert.Trim().ToUpperInvariant().Replace("-", "")}" };
        }

        if (!_crawler.Configured)
        {
            _log.LogWarning("Arbitrary-topic generation requested but crawler is not configured (Crawler:Url/Secret missing)");
            return new List<string>();
        }

        List<CrawlerClient.CrawledDocument> docs;
        try
        {
            docs = await _crawler.SearchAsync(prompt, limit: 5, ct);
        }
        catch (Exception ex)
        {
            // Discovery failure (crawler down, no Firecrawl key, unsafe results...)
            // surfaces as the clear "couldn't find source material" job failure,
            // never as a generic AI error.
            _log.LogWarning(ex, "Discovery failed for '{Topic}'", prompt);
            return new List<string>();
        }
        _log.LogInformation("Discovery for '{Topic}': {Count} candidate documents", prompt, docs.Count);
        if (docs.Count == 0) return new List<string>();

        var namespaces = new List<string>();
        foreach (var doc in docs)
        {
            if (string.IsNullOrWhiteSpace(doc.Markdown)) continue;
            var source = await _sources.IngestAsync(doc.CanonicalUrl, doc.Title, doc.Markdown, doc.ContentHash, ct);
            if (source is null) continue;
            namespaces.Add(SourceService.NamespaceFor(source.Id));
        }
        return namespaces;
    }
}
