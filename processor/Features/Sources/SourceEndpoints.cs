using EzCert.Processor.Features.Sources;
using EzCert.Processor.Infrastructure.CrawlerClient;

namespace EzCert.Processor.Features.Sources;

// Source attachment endpoints (Chat config sheet):
//   POST /api/sources/upload  (multipart "files") -> [{ sourceId, title, chunkCount }]
//   POST /api/sources/crawl   { urls: [...] }      -> [{ url, sourceId?, title, chunkCount?, error? }]
// Files are chunked + embedded immediately (SourceService); the raw file is
// discarded — Qdrant chunks + the Source row are the source of truth.
public static class SourceEndpoints
{
    private static readonly string[] AllowedExtensions = { ".txt", ".md", ".markdown" };
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10 MB

    public static IEndpointRouteBuilder MapSourceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sources/upload", async (IFormFileCollection files, SourceService sources, HttpContext ctx) =>
        {
            var results = new List<object>();
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                {
                    results.Add(new { fileName = file.FileName, error = "Only .txt and .md files are supported." });
                    continue;
                }
                if (file.Length > MaxFileBytes)
                {
                    results.Add(new { fileName = file.FileName, error = "File exceeds the 10 MB limit." });
                    continue;
                }

                using var reader = new StreamReader(file.OpenReadStream());
                var text = await reader.ReadToEndAsync(ctx.RequestAborted);
                if (string.IsNullOrWhiteSpace(text))
                {
                    results.Add(new { fileName = file.FileName, error = "File is empty." });
                    continue;
                }

                var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
                var (source, chunkCount) = await sources.IngestAsync($"upload://{file.FileName}", file.FileName, text, hash, ctx.RequestAborted);
                if (source is null)
                {
                    results.Add(new { fileName = file.FileName, error = "Could not ingest this file." });
                    continue;
                }
                results.Add(new { fileName = file.FileName, sourceId = source.Id, title = source.Title, chunkCount });
            }
            return Results.Ok(results);
        }).DisableAntiforgery();

        app.MapPost("/api/sources/crawl", async (CrawlRequest req, CrawlerClient crawler, SourceService sources, HttpContext ctx) =>
        {
            if (req.Urls is null || req.Urls.Count == 0)
                return Results.BadRequest(new { error = "urls is required" });

            var results = new List<object>();
            foreach (var rawUrl in req.Urls.Take(10))
            {
                var url = rawUrl.Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    results.Add(new { url, error = "Invalid URL." });
                    continue;
                }
                try
                {
                    var docs = await crawler.CrawlAsync(url, limit: 10, ctx.RequestAborted);
                    var ingested = new List<Guid>();
                    foreach (var doc in docs)
                    {
                        if (string.IsNullOrWhiteSpace(doc.Markdown)) continue;
                        var (source, chunkCount) = await sources.IngestAsync(doc.CanonicalUrl, doc.Title, doc.Markdown, doc.ContentHash, ctx.RequestAborted);
                        if (source is not null && !ingested.Contains(source.Id))
                        {
                            ingested.Add(source.Id);
                            results.Add(new { url = doc.CanonicalUrl, sourceId = source.Id, title = source.Title, chunkCount });
                        }
                    }
                    if (ingested.Count == 0)
                        results.Add(new { url, error = "No usable content found at this URL." });
                }
                catch (Exception ex)
                {
                    results.Add(new { url, error = ex.Message.Length > 120 ? ex.Message[..120] : ex.Message });
                }
            }
            return Results.Ok(results);
        });

        return app;
    }

    private sealed record CrawlRequest(List<string>? Urls);
}
