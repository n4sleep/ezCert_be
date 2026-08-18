using EzCert.Processor.Infrastructure.Bedrock;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace EzCert.Processor.Features.Sources;

// Seeds official certification content into Qdrant (AD-6/AD-9).
// Official sources: namespace official:{cert}; chunks carry { namespace, sourceUrl, section, text }.
// Run via `dotnet run -- seed` or hosted once at startup when the collection is empty.
public class SeedService
{
    public const string Collection = "ezcert";
    public const int Dims = 1024; // Titan embed v2

    private readonly QdrantClient _qdrant;
    private readonly IBedrockClient _bedrock;
    private readonly ILogger<SeedService> _log;

    public SeedService(QdrantClient qdrant, IBedrockClient bedrock, ILogger<SeedService> log)
    {
        _qdrant = qdrant;
        _bedrock = bedrock;
        _log = log;
    }

    // Loads processor/seed/official/{cert}/*.md -> chunks -> embed -> upsert.
    public async Task<int> SeedOfficialAsync(string cert, string seedDir, CancellationToken ct = default)
    {
        if (!IsValidCert(cert))
        {
            _log.LogWarning("Seed skipped: invalid cert code '{Cert}' (must be alphanumeric, e.g. AZ-900)", cert);
            return 0;
        }
        var dir = Path.Combine(seedDir, cert.ToLowerInvariant());
        if (!Directory.Exists(dir))
        {
            _log.LogWarning("Seed directory not found: {Dir}", dir);
            return 0;
        }

        // collection (create if missing, sized for Titan 1024)
        var collections = await _qdrant.ListCollectionsAsync(ct);
        if (!collections.Contains(Collection))
            await _qdrant.CreateCollectionAsync(Collection, new VectorParams { Size = Dims, Distance = Distance.Cosine }, cancellationToken: ct);

        var ns = $"official:{NormalizeCert(cert)}";
        var points = new List<PointStruct>();
        var total = 0;

        foreach (var file in Directory.GetFiles(dir, "*.md"))
        {
            var markdown = await File.ReadAllTextAsync(file, ct);
            var sourceUrl = TextChunker.ExtractCanonicalUrl(markdown) ?? $"file://{Path.GetFileName(file)}";
            var sourceTitle = TextChunker.ExtractTitle(markdown) ?? Path.GetFileNameWithoutExtension(file);
            var ordinal = 0;
            foreach (var chunk in TextChunker.Chunk(markdown, 700, 80))
            {
                // Stable point IDs (FNV-1a over namespace + chunk text): re-seeding
                // the same cert is an idempotent upsert, and seeding a second cert
                // can never overwrite the first one's points (namespaces differ).
                var id = StableId(ns, chunk.Text);
                var emb = await _bedrock.EmbedAsync(chunk.Text, ct);
                points.Add(new PointStruct
                {
                    Id = id,
                    Vectors = emb,
                    Payload =
                    {
                        ["namespace"] = ns,
                        ["source_url"] = sourceUrl,
                        ["source_title"] = sourceTitle,
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
        }

        if (points.Count > 0)
            await _qdrant.UpsertAsync(Collection, points, cancellationToken: ct);

        _log.LogInformation("Seeded {Count} chunks for {Ns}", total, ns);
        return total;
    }

    // True when the namespace has at least one point (used for per-cert idempotency).
    public async Task<bool> NamespaceExistsAsync(string ns, CancellationToken ct = default)
    {
        var result = await _qdrant.ScrollAsync(
            Collection,
            limit: 1,
            payloadSelector: new Qdrant.Client.Grpc.WithPayloadSelector { Enable = false },
            filter: Qdrant.Client.Grpc.Conditions.MatchKeyword("namespace", ns),
            cancellationToken: ct);
        return result.Result?.Count > 0;
    }

    private static bool IsValidCert(string cert) =>
        !string.IsNullOrWhiteSpace(cert) && System.Text.RegularExpressions.Regex.IsMatch(cert, @"^[A-Za-z0-9-]+$");

    // FNV-1a 64-bit over UTF-8 bytes of namespace + chunk text -> stable point ID.
    public static ulong StableId(string ns, string text)
    {
        const ulong fnvOffset = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;
        var hash = fnvOffset;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes($"{ns}\u0000{text}"))
        {
            hash ^= b;
            hash *= fnvPrime;
        }
        return hash;
    }

    private static string NormalizeCert(string cert) => cert.Trim().ToUpperInvariant().Replace("-", ""); // az-900 -> AZ900

}
