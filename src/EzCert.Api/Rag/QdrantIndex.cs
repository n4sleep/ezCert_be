using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace EzCert.Api.Rag;

public sealed record SearchHit(string Text, string Source, string Slug, float Score, int Ordinal);

public interface IQdrantIndex
{
    Task EnsureCollectionAsync(bool recreate = false, CancellationToken ct = default);
    Task UpsertAsync(IEnumerable<RagChunk> chunks, Func<RagChunk, float[]> vectorOf, CancellationToken ct = default);
    Task<IReadOnlyList<SearchHit>> SearchAsync(float[] vector, int limit, string? slug = null, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
}

public sealed class QdrantIndex : IQdrantIndex
{
    private readonly QdrantClient _client;
    private readonly RagOptions _opt;
    private readonly ILogger<QdrantIndex> _log;

    public QdrantIndex(QdrantClient client, IOptions<RagOptions> opt, ILogger<QdrantIndex> log)
    {
        _client = client;
        _opt = opt.Value;
        _log = log;
    }

    public async Task EnsureCollectionAsync(bool recreate = false, CancellationToken ct = default)
    {
        var exists = await _client.CollectionExistsAsync(_opt.Collection, ct);
        if (exists && recreate)
        {
            await _client.DeleteCollectionAsync(_opt.Collection, cancellationToken: ct);
            exists = false;
        }
        if (!exists)
        {
            await _client.CreateCollectionAsync(
                _opt.Collection,
                new VectorParams { Size = (ulong)_opt.EmbeddingDim, Distance = Distance.Cosine },
                cancellationToken: ct);
            _log.LogInformation("Created Qdrant collection {Name} (dim {Dim})", _opt.Collection, _opt.EmbeddingDim);
        }
    }

    public async Task UpsertAsync(IEnumerable<RagChunk> chunks, Func<RagChunk, float[]> vectorOf, CancellationToken ct = default)
    {
        var points = new List<PointStruct>();
        foreach (var c in chunks)
        {
            var p = new PointStruct
            {
                Id = StableId(c.SectionSlug, c.Ordinal),
                Vectors = vectorOf(c)
            };
            p.Payload.Add("slug", c.SectionSlug);
            p.Payload.Add("source", c.SourceUrl);
            p.Payload.Add("text", c.Content);
            p.Payload.Add("ordinal", (long)c.Ordinal);
            points.Add(p);
        }
        if (points.Count == 0) return;
        await _client.UpsertAsync(_opt.Collection, points, cancellationToken: ct);
        _log.LogInformation("Upserted {Count} points into {Name}", points.Count, _opt.Collection);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(float[] vector, int limit, string? slug = null, CancellationToken ct = default)
    {
        Filter? filter = null;
        if (!string.IsNullOrWhiteSpace(slug))
        {
            filter = new Filter();
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition { Key = "slug", Match = new Match { Keyword = slug } }
            });
        }

        var results = await _client.SearchAsync(
            _opt.Collection, vector, filter: filter, limit: (ulong)limit, cancellationToken: ct);

        return results.Select(p => new SearchHit(
            Text: p.Payload.TryGetValue("text", out var t) ? t.StringValue : "",
            Source: p.Payload.TryGetValue("source", out var s) ? s.StringValue : "",
            Slug: p.Payload.TryGetValue("slug", out var sl) ? sl.StringValue : "",
            Score: p.Score,
            Ordinal: p.Payload.TryGetValue("ordinal", out var o) ? (int)o.IntegerValue : 0
        )).ToList();
    }

    public async Task<long> CountAsync(CancellationToken ct = default)
    {
        var exists = await _client.CollectionExistsAsync(_opt.Collection, ct);
        if (!exists) return 0;
        return (long)await _client.CountAsync(_opt.Collection, cancellationToken: ct);
    }

    // Deterministic GUID from slug+ordinal so re-indexing overwrites rather than duplicates.
    private static Guid StableId(string slug, int ordinal)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"{slug}:{ordinal}"));
        return new Guid(hash);
    }
}
