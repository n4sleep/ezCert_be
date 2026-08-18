using EzCert.Processor.Infrastructure.Bedrock;
using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace EzCert.Processor.Features.Sources;

// Stable source ingestion (WS-3B): crawled/discovered documents are indexed
// ONCE under namespace "source:{Source.Id}" (AD-4 Source model) and reused on
// repeat topics via contentHash dedupe. Chunking/embedding reuse the same
// pipeline as seeds.
public class SourceService
{
    private readonly QdrantClient _qdrant;
    private readonly IBedrockClient _bedrock;
    private readonly EzCertDbContext _db;
    private readonly ILogger<SourceService> _log;

    public SourceService(QdrantClient qdrant, IBedrockClient bedrock, EzCertDbContext db, ILogger<SourceService> log)
    {
        _qdrant = qdrant;
        _bedrock = bedrock;
        _db = db;
        _log = log;
    }

    public const string Collection = SeedService.Collection;

    public static string NamespaceFor(Guid sourceId) => $"source:{sourceId:N}";

    // Returns the existing Source when the content hash is already ingested,
    // otherwise ingests and returns the new Source. Null on empty content.
    public async Task<Source?> IngestAsync(string url, string title, string markdown, string contentHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return null;

        var existing = await _db.SourceDocuments
            .Include(d => d.Source)
            .FirstOrDefaultAsync(d => d.ContentHash == contentHash, ct);
        if (existing?.Source is not null)
        {
            _log.LogInformation("Source reuse: hash {Hash} already indexed as {SourceId}", contentHash, existing.Source.Id);
            return existing.Source;
        }

        var source = new Source
        {
            Kind = "url",
            Title = string.IsNullOrWhiteSpace(title) ? url : title,
            Status = "ready",
        };
        var doc = new SourceDocument
        {
            Source = source,
            CanonicalUrl = url,
            ContentHash = contentHash,
            FetchedAt = DateTime.UtcNow,
        };
        source.Documents.Add(doc);
        _db.Sources.Add(source);
        await _db.SaveChangesAsync(ct);

        var ns = NamespaceFor(source.Id);
        var points = new List<PointStruct>();
        var total = 0;
        var ordinal = 0;
        foreach (var chunk in TextChunker.Chunk(markdown, 700, 80))
        {
            var emb = await _bedrock.EmbedAsync(chunk.Text, ct);
            points.Add(new PointStruct
            {
                Id = SeedService.StableId(ns, chunk.Text),
                Vectors = emb,
                Payload =
                {
                    ["namespace"] = ns,
                    ["source_url"] = url,
                    ["source_title"] = source.Title,
                    ["section"] = chunk.Section,
                    ["text"] = chunk.Text,
                    ["ordinal"] = ordinal++,
                },
            });
            if (points.Count >= 32)
            {
                await _qdrant.UpsertAsync(Collection, points, cancellationToken: ct);
                points.Clear();
            }
            total++;
        }

        if (points.Count > 0)
            await _qdrant.UpsertAsync(Collection, points, cancellationToken: ct);

        _log.LogInformation("Ingested source {SourceId} ({Total} chunks, ns={Ns})", source.Id, total, ns);
        return source;
    }
}
