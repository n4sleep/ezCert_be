using System.Text.Json;
using System.Text.RegularExpressions;
using EzCert.Processor.Features.Exams;
using EzCert.Processor.Features.Sources;
using EzCert.Processor.Infrastructure.Bedrock;
using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Qdrant.Client;

namespace EzCert.Processor.Features.Generation;

// Real generation pipeline (AD-7): embed prompt -> retrieve official chunks
// from Qdrant -> Bedrock generates a JSON exam grounded in the chunks ->
// validate + retry (<=3) -> persist Exam ready. Replaces the MiniBank slice.
public class GenerationService
{
    private readonly QdrantClient _qdrant;
    private readonly IBedrockClient _bedrock;
    private readonly EzCertDbContext _db;
    private readonly ILogger<GenerationService> _log;

    public GenerationService(QdrantClient qdrant, IBedrockClient bedrock, EzCertDbContext db, ILogger<GenerationService> log)
    {
        _qdrant = qdrant;
        _bedrock = bedrock;
        _db = db;
        _log = log;
    }

    public async Task<Exam?> GenerateAsync(string deviceId, string prompt, string? configJson, IReadOnlyList<string> namespaces, Func<string, Task>? onStage = null, CancellationToken ct = default)
    {
        var cfg = ParseConfig(configJson);
        var count = cfg.Count is > 0 and <= 20 ? cfg.Count!.Value : 5;
        var cert = string.IsNullOrWhiteSpace(cfg.Cert) ? DetectCert(prompt) : cfg.Cert!;
        var difficulty = string.IsNullOrWhiteSpace(cfg.Difficulty) ? DetectDifficulty(prompt) : cfg.Difficulty!;
        var mode = cfg.Mode == "certification" ? "certification" : "practice";

        if (onStage is not null) await onStage("embedding");
        var query = await _bedrock.EmbedAsync(prompt, ct);

        // Evidence-oriented quality gate (WS-3B): enough usable chunks to cover
        // the requested question count, from at least one distinct source.
        var minChunks = Math.Max(2, (int)Math.Ceiling(count / 2.0));
        var hits = await SearchAsync(namespaces, query, ct);
        var evidence = BuildEvidence(hits);
        var distinctSources = evidence.Select(e => e.Url).Distinct().Count();
        _log.LogWarning("RAG search: {Hits} hits, {Evidence} evidence blocks, {Sources} distinct sources, ns={Ns}",
            hits.Count, evidence.Count, distinctSources, string.Join("|", namespaces));
        if (evidence.Count == 0)
            _log.LogWarning("No Qdrant chunks retrieved for {Ns} (collection may need seeding)", string.Join("|", namespaces));

        if (evidence.Count < minChunks || distinctSources < 1)
        {
            _log.LogWarning("Evidence gate failed: {Evidence} evidence blocks (< {MinChunks} needed, {Sources} sources)",
                evidence.Count, minChunks, distinctSources);
            return null;
        }

        // Request-local evidence IDs (E1..En) — raw Qdrant point IDs never leave
        // the server (provenance constraint). The mapping lives in this scope only.
        var context = string.Join("\n\n", evidence.Select(e =>
            $"[EVIDENCE: {e.Id}]\nSource: {e.Url}\nTitle: {e.Title}\nSection: {e.Section}\nText:\n{e.Text}"));
        var system = "You are an exam generator. Generate questions STRICTLY grounded in the provided EVIDENCE blocks. " +
                     "Each question MUST reference at least one evidence ID. " +
                     "Return ONLY a JSON object, no markdown fences, no commentary. " +
                     "In the JSON, set sourceUrl to an empty string \"\" — the system attaches the trusted source.";
        var user = BuildPrompt(prompt, count, cert, difficulty, mode, context);

        if (onStage is not null) await onStage("generating");
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            if (onStage is not null) await onStage(attempt > 1 ? "generating" : "validating");
            var raw = await _bedrock.GenerateAsync(user, system, 2500, 0.3f, ct);
            _log.LogDebug("Generation attempt {Attempt} raw output: {Raw}", attempt, raw);
            var exam = await TryPersistAsync(deviceId, prompt, cert, difficulty, mode, count, cfg.Title, cfg.DurationMinutes, raw, evidence, ct);
            if (exam is not null)
            {
                _log.LogInformation("Generation ok on attempt {Attempt} ({Count} questions)", attempt, exam.Questions.Count);
                return exam;
            }
            _log.LogWarning("Generation attempt {Attempt} failed validation", attempt);
        }

        return null;
    }

    private string BuildPrompt(string prompt, int count, string cert, string difficulty, string mode, string context)
    {
        return $$"""
        Create a {{mode}} practice exam for {{cert}} at {{difficulty}} difficulty with exactly {{count}} questions.

        USER REQUEST: "{{prompt}}"

        EVIDENCE BLOCKS (ground EVERY question in these; cite via evidenceIds):
        {{context}}

        Respond with a JSON object of this exact shape:
        {
          "title": "string",
          "questions": [
            {
              "type": "single" | "multi" | "truefalse",
              "text": "question text",
              "choices": [ { "label": "a", "text": "choice text" } ],
              "correct": ["a"],
              "explanation": "2-3 sentence explanation",
              "section": "short semantic topic area",
              "topic": "narrower subject",
              "evidenceIds": ["E1", "E3"]
            }
          ]
        }

        Rules:
        - exactly {{count}} questions
        - 2 choices for truefalse, 3-4 for single, 4 for multi (multi has 2+ correct)
        - every explanation must reference the evidence blocks
        - evidenceIds MUST reference only IDs present in the EVIDENCE BLOCKS (E1, E2, ...)
        - at least one evidenceIds entry per question; use more than one when the question draws on multiple blocks
        - sourceUrl is never present in the output
        """;
    }

    // Retrieval across one or more namespaces (official:{CERT} and/or source:{id}).
    private async Task<IReadOnlyList<Qdrant.Client.Grpc.ScoredPoint>> SearchAsync(IReadOnlyList<string> namespaces, ReadOnlyMemory<float> query, CancellationToken ct)
    {
        if (namespaces.Count == 1)
        {
            return await _qdrant.SearchAsync(
                SeedService.Collection,
                query,
                limit: 8,
                payloadSelector: new Qdrant.Client.Grpc.WithPayloadSelector { Enable = true },
                filter: Qdrant.Client.Grpc.Conditions.MatchKeyword("namespace", namespaces[0]),
                cancellationToken: ct);
        }

        var filter = new Qdrant.Client.Grpc.Filter
        {
            Should = { namespaces.Select(ns => Qdrant.Client.Grpc.Conditions.MatchKeyword("namespace", ns)) },
        };
        return await _qdrant.SearchAsync(
            SeedService.Collection,
            query,
            limit: 8,
            payloadSelector: new Qdrant.Client.Grpc.WithPayloadSelector { Enable = true },
            filter: filter,
            cancellationToken: ct);
    }

    // Maps retrieved Qdrant hits to request-local evidence IDs. Raw point IDs are
    // never exposed to the LLM or persisted.
    private sealed class EvidenceBlock
    {
        public required string Id { get; init; }
        public required string Url { get; init; }
        public required string Title { get; init; }
        public required string Section { get; init; }
        public required string Text { get; init; }
    }

    private static List<EvidenceBlock> BuildEvidence(IReadOnlyList<Qdrant.Client.Grpc.ScoredPoint> hits)
    {
        var list = new List<EvidenceBlock>();
        for (var i = 0; i < hits.Count; i++)
        {
            var p = hits[i].Payload;
            var url = (p.TryGetValue("source_url", out var u) ? u.StringValue : "")?.Trim().TrimEnd('/') ?? "";
            var text = p.TryGetValue("text", out var t) ? t.StringValue : "";
            var title = p.TryGetValue("source_title", out var st) ? st.StringValue : "";
            var section = p.TryGetValue("section", out var s) ? s.StringValue : "";
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(text)) continue;
            list.Add(new EvidenceBlock
            {
                Id = $"E{i + 1}",
                Url = url,
                Title = title,
                Section = section,
                Text = text,
            });
        }
        return list;
    }

    private async Task<Exam?> TryPersistAsync(string deviceId, string prompt, string cert, string difficulty, string mode, int count, string? configTitle, int? configDuration, string raw, List<EvidenceBlock> evidence, CancellationToken ct)
    {
        try
        {
            var doc = JsonDocument.Parse(StripFences(raw));
            if (!doc.RootElement.TryGetProperty("questions", out var qs) || qs.GetArrayLength() == 0)
                return null;
            var title = configTitle;
            if (string.IsNullOrWhiteSpace(title))
            {
                title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : $"{cert} Practice";
                if (string.IsNullOrWhiteSpace(title)) title = $"{cert} Practice";
            }

            var exam = new Exam
            {
                OwnerDeviceId = deviceId,
                Title = title,
                Description = $"Generated from: {prompt}",
                CertificationCode = cert.ToUpperInvariant(),
                Mode = mode,
                Difficulty = difficulty,
                DurationMinutes = configDuration is > 0 ? Math.Min(configDuration.Value, 60) : Math.Clamp(count * 2, 5, 60),
                PassPercent = 70,
                Status = "ready",
                GenerationPrompt = prompt,
                ExpiresAt = DateTime.UtcNow.AddDays(3),
                ShareToken = null,
            };

            var ord = 0;
            var seenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var q in qs.EnumerateArray())
            {
                if (!q.TryGetProperty("text", out var qText) || string.IsNullOrWhiteSpace(qText.GetString()))
                    return null;
                var text = qText.GetString()!.Trim();
                if (text.Length < 10 || text.Length > 1000)
                    return null; // unreasonable question length
                if (!seenTexts.Add(NormalizeForDedupe(text)))
                    return null; // duplicate question
                if (!q.TryGetProperty("choices", out var choices) || choices.GetArrayLength() < 2)
                    return null;
                if (!q.TryGetProperty("correct", out var correct) || correct.GetArrayLength() == 0)
                    return null;

                var choiceList = new List<Choice>();
                var labels = new HashSet<string>();
                var cOrd = 0;
                foreach (var c in choices.EnumerateArray())
                {
                    if (!c.TryGetProperty("label", out var lab) || !c.TryGetProperty("text", out var cText))
                        return null;
                    var label = lab.GetString()?.Trim() ?? "";
                    var choiceText = cText.GetString()?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(label) || !labels.Add(label))
                        return null;
                    if (string.IsNullOrWhiteSpace(choiceText))
                        return null; // empty choice text
                    choiceList.Add(new Choice
                    {
                        Label = label,
                        Text = choiceText,
                        IsCorrect = correct.EnumerateArray().Any(x => x.GetString() == label),
                        Ordinal = cOrd++,
                    });
                }

                var type = q.TryGetProperty("type", out var ty) ? ty.GetString() : null;
                if (type is not null and not ("single" or "multi" or "truefalse"))
                    return null; // invalid type -> reject, never silently repair
                type ??= choiceList.Count(c => c.IsCorrect) > 1 ? "multi" : "single";

                // Type-consistency rules (WS-5): exact correct-answer counts.
                var correctCount = choiceList.Count(c => c.IsCorrect);
                if (type == "single" && correctCount != 1) return null;
                if (type == "truefalse" && (choiceList.Count != 2 || correctCount != 1)) return null;
                if (type == "multi" && correctCount < 2) return null;

                var explanation = q.TryGetProperty("explanation", out var ex) ? ex.GetString()?.Trim() ?? "" : "";
                if (explanation.Length < 20)
                    return null; // non-empty, substantive explanation required

                var question = new Question
                {
                    Ordinal = ord++,
                    Type = type,
                    Text = text,
                    Explanation = explanation,
                    Section = q.TryGetProperty("section", out var sec) && !string.IsNullOrWhiteSpace(sec.GetString())
                        ? sec.GetString()!
                        : evidence.FirstOrDefault()?.Section ?? "general",
                    Topic = q.TryGetProperty("topic", out var top) ? top.GetString() ?? "" : "",
                };
                question.Choices.AddRange(choiceList);

                // Evidence provenance: resolve evidenceIds against the retrieved
                // context; invalid or missing references reject the question.
                var refs = new List<EvidenceBlock>();
                if (q.TryGetProperty("evidenceIds", out var evIds) && evIds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ev in evIds.EnumerateArray())
                    {
                        var id = ev.GetString();
                        var match = id is not null ? evidence.FirstOrDefault(e => e.Id == id) : null;
                        if (match is null) return null; // invented/unknown evidence id -> reject
                        if (refs.All(r => r.Id != match.Id)) refs.Add(match);
                    }
                }
                if (refs.Count == 0) return null; // no grounded evidence -> reject

                foreach (var ev in refs)
                {
                    question.Citations.Add(new QuestionCitation
                    {
                        SourceDocumentId = null, // web/official docs have no source row yet (WS-3B adds source:<id>)
                        SourceUrl = ev.Url,
                        SourceTitle = string.IsNullOrWhiteSpace(ev.Title) ? null : ev.Title,
                        Section = string.IsNullOrWhiteSpace(ev.Section) ? null : ev.Section,
                        QuotedText = ev.Text, // actual retrieved passage, never the AI explanation
                    });
                }

                exam.Questions.Add(question);
            }

            if (exam.Questions.Count != count)
                return null;

            _db.Exams.Add(exam);
            await _db.SaveChangesAsync(ct);
            return exam;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string StripFences(string raw)
    {
        var m = Regex.Match(raw, @"```(?:json)?\s*([\s\S]*?)```");
        return m.Success ? m.Groups[1].Value.Trim() : raw.Trim();
    }

    private static string NormalizeForDedupe(string text)
    {
        return Regex.Replace(text.ToLowerInvariant().Trim(), @"\s+", " ");
    }

    private static string DetectCert(string prompt)
    {
        var m = Regex.Match(prompt, @"\b(AZ-900|CLF-C02|AI-900|DP-900)\b", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : "AZ-900";
    }

    private static string DetectDifficulty(string prompt)
    {
        if (Regex.IsMatch(prompt, @"dễ|easy|basic", RegexOptions.IgnoreCase)) return "easy";
        if (Regex.IsMatch(prompt, @"khó|hard|difficult|nâng cao", RegexOptions.IgnoreCase)) return "hard";
        return "medium";
    }

    private static readonly JsonSerializerOptions ConfigJson = new(JsonSerializerDefaults.Web);

    private static GenerationConfig ParseConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return new GenerationConfig();
        try
        {
            // Web defaults: camelCase JSON matching the PascalCase properties.
            return JsonSerializer.Deserialize<GenerationConfig>(configJson, ConfigJson) ?? new GenerationConfig();
        }
        catch (JsonException)
        {
            return new GenerationConfig();
        }
    }

    private sealed class GenerationConfig
    {
        public int? Count { get; set; }
        public string? Cert { get; set; }
        public string? Difficulty { get; set; }
        public string? Mode { get; set; }
        public string? Title { get; set; }
        public int? DurationMinutes { get; set; }
        public List<string>? SourceIds { get; set; }
        public bool? AutoCrawl { get; set; }
    }
}
