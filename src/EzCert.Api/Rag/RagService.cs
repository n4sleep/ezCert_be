using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace EzCert.Api.Rag;

public sealed record IndexResult(int Files, int Chunks, long TotalPoints);
public sealed record GeneratedOption(string Key, string Text, bool IsCorrect);
public sealed record GeneratedQuestion(string Stem, string Type, List<GeneratedOption> Options, string Explanation, string Source);

public sealed class RagService
{
    private readonly IBedrockClient _bedrock;
    private readonly IQdrantIndex _index;
    private readonly RagOptions _opt;
    private readonly IHostEnvironment _env;
    private readonly ILogger<RagService> _log;

    public RagService(IBedrockClient bedrock, IQdrantIndex index, IOptions<RagOptions> opt, IHostEnvironment env, ILogger<RagService> log)
    {
        _bedrock = bedrock;
        _index = index;
        _opt = opt.Value;
        _env = env;
        _log = log;
    }

    // Resolves the crawl/out directory. Absolute config path wins; otherwise walk up
    // from the content root looking for a "crawl/out" folder.
    private string ResolveCrawlPath(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;
        if (Path.IsPathRooted(_opt.CrawlPath) && Directory.Exists(_opt.CrawlPath)) return _opt.CrawlPath;

        var dir = _env.ContentRootPath;
        for (var i = 0; i < 6 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "crawl", "out");
            if (Directory.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Path.Combine(_env.ContentRootPath, _opt.CrawlPath);
    }

    private string SlugFor(string fileStem) =>
        _opt.SlugMap.TryGetValue(fileStem, out var slug) ? slug : fileStem;

    public async Task<IndexResult> IndexAsync(bool recreate, string? pathOverride, CancellationToken ct = default)
    {
        var root = ResolveCrawlPath(pathOverride);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Crawl path not found: {root}");

        await _index.EnsureCollectionAsync(recreate, ct);

        var files = Directory.GetFiles(root, "*.md")
            .Where(f => !Path.GetFileNameWithoutExtension(f).Equals("index", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var totalChunks = 0;
        foreach (var file in files)
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            var slug = SlugFor(stem);
            var md = await File.ReadAllTextAsync(file, ct);
            var chunks = MarkdownChunker.Chunk(slug, md, _opt.MaxChunkChars);
            if (chunks.Count == 0) continue;

            // Embed sequentially to stay well under Bedrock throughput limits.
            var vectors = new Dictionary<int, float[]>();
            foreach (var c in chunks)
                vectors[c.Ordinal] = await _bedrock.EmbedAsync(c.Content, ct);

            await _index.UpsertAsync(chunks, c => vectors[c.Ordinal], ct);
            totalChunks += chunks.Count;
            _log.LogInformation("Indexed {File} -> slug {Slug}: {Count} chunks", stem, slug, chunks.Count);
        }

        var total = await _index.CountAsync(ct);
        return new IndexResult(files.Count, totalChunks, total);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int? limit, string? slug, CancellationToken ct = default)
    {
        var vector = await _bedrock.EmbedAsync(query, ct);
        return await _index.SearchAsync(vector, limit ?? _opt.SearchLimit, slug, ct);
    }

    private static string BuildContext(IReadOnlyList<SearchHit> hits)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < hits.Count; i++)
        {
            sb.AppendLine($"[Context {i + 1}] (source: {hits[i].Source})");
            sb.AppendLine(hits[i].Text);
            sb.AppendLine();
        }
        return sb.ToString().Trim();
    }

    private const string GenSystem =
        "You are an exam item writer for the Microsoft AZ-900 Azure Fundamentals certification. " +
        "Write one high-quality multiple-choice question grounded ONLY in the supplied context. " +
        "Never invent facts beyond the context. Respond with STRICT JSON only, no markdown fences.";

    public async Task<GeneratedQuestion> GenerateQuestionAsync(string topic, string? slug, CancellationToken ct = default)
    {
        var hits = await SearchAsync(topic, _opt.SearchLimit, slug, ct);
        if (hits.Count == 0)
            throw new InvalidOperationException("No indexed context found. Run indexing first.");

        var context = BuildContext(hits);
        var schema =
            "Return a JSON object with EXACTLY this shape:\n" +
            "{\"stem\":\"...\",\"type\":\"single|multi|truefalse\"," +
            "\"options\":[{\"key\":\"a\",\"text\":\"...\",\"isCorrect\":true}]," +
            "\"explanation\":\"why correct, grounded in context\",\"source\":\"a source URL from the context\"}\n" +
            "Rules: single/multi have 3-5 options; truefalse has exactly 2 (True/False). " +
            "At least one option isCorrect=true; multi has 2+ correct. Keys are lowercase letters a,b,c,...";

        string? feedback = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var prompt =
                $"Context:\n{context}\n\nTopic to test: {topic}\n\n{schema}" +
                (feedback is null ? "" : $"\n\nYour previous attempt was rejected: {feedback}. Fix it.");

            var raw = await _bedrock.GenerateAsync(prompt, GenSystem, maxTokens: 700, temperature: 0.3f, ct: ct);
            var json = ExtractJson(raw);
            if (json is null) { feedback = "output was not valid JSON"; continue; }

            GeneratedQuestion? q;
            try { q = Parse(json); }
            catch (Exception ex) { feedback = $"JSON did not match schema ({ex.Message})"; continue; }

            var error = Validate(q);
            if (error is null)
            {
                _log.LogInformation("Generated question on attempt {Attempt} for slug {Slug}", attempt, slug ?? "(any)");
                return q;
            }
            feedback = error;
            _log.LogWarning("Generated question rejected (attempt {Attempt}): {Error}", attempt, error);
        }
        throw new InvalidOperationException($"Failed to generate a valid question after retries: {feedback}");
    }

    private static GeneratedQuestion Parse(string json)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dto = JsonSerializer.Deserialize<GenDto>(json, opts)
                  ?? throw new JsonException("null");
        var options = (dto.Options ?? new()).Select(o => new GeneratedOption(o.Key ?? "", o.Text ?? "", o.IsCorrect)).ToList();
        return new GeneratedQuestion(dto.Stem ?? "", (dto.Type ?? "").ToLowerInvariant(), options, dto.Explanation ?? "", dto.Source ?? "");
    }

    private static string? Validate(GeneratedQuestion q)
    {
        if (string.IsNullOrWhiteSpace(q.Stem)) return "stem is empty";
        if (q.Type is not ("single" or "multi" or "truefalse")) return $"invalid type '{q.Type}'";
        if (q.Options.Count < 2) return "need at least 2 options";
        if (q.Type == "truefalse" && q.Options.Count != 2) return "truefalse must have exactly 2 options";
        var correct = q.Options.Count(o => o.IsCorrect);
        if (correct < 1) return "no correct option";
        if (q.Type == "multi" && correct < 2) return "multi must have 2+ correct options";
        if (q.Type == "single" && correct != 1) return "single must have exactly 1 correct option";
        if (q.Options.Any(o => string.IsNullOrWhiteSpace(o.Text))) return "an option has empty text";
        return null;
    }

    private static string? ExtractJson(string s)
    {
        var start = s.IndexOf('{');
        var end = s.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return s.Substring(start, end - start + 1);
    }

    public async Task<string> ExplainAsync(string question, string correctAnswer, string? userAnswer, string? slug, CancellationToken ct = default)
    {
        var hits = await SearchAsync($"{question} {correctAnswer}", _opt.SearchLimit, slug, ct);
        var context = hits.Count > 0 ? BuildContext(hits) : "(no additional context available)";
        var system =
            "You are a friendly Azure certification tutor. Explain concisely (2-4 sentences) using only the context. " +
            "Be encouraging and factual.";
        var wrong = string.IsNullOrWhiteSpace(userAnswer)
            ? ""
            : $"\nThe learner answered: {userAnswer}. If that is incorrect, gently explain why.";
        var prompt =
            $"Context:\n{context}\n\nQuestion: {question}\nCorrect answer: {correctAnswer}{wrong}\n\n" +
            "Explain why the correct answer is right.";
        return await _bedrock.GenerateAsync(prompt, system, maxTokens: 400, temperature: 0.3f, ct: ct);
    }

    private sealed class GenDto
    {
        public string? Stem { get; set; }
        public string? Type { get; set; }
        public List<GenOpt>? Options { get; set; }
        public string? Explanation { get; set; }
        public string? Source { get; set; }
    }

    private sealed class GenOpt
    {
        public string? Key { get; set; }
        public string? Text { get; set; }
        public bool IsCorrect { get; set; }
    }
}
