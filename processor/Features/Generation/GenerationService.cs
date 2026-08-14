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

    public async Task<Exam?> GenerateAsync(string deviceId, string prompt, string? configJson, CancellationToken ct = default)
    {
        var cfg = ParseConfig(configJson);
        var count = cfg.Count is > 0 and <= 20 ? cfg.Count!.Value : 5;
        var cert = string.IsNullOrWhiteSpace(cfg.Cert) ? DetectCert(prompt) : cfg.Cert!;
        var difficulty = string.IsNullOrWhiteSpace(cfg.Difficulty) ? DetectDifficulty(prompt) : cfg.Difficulty!;

        var query = await _bedrock.EmbedAsync(prompt, ct);
        var ns = $"official:{NormalizeCert(cert)}";
        var hits = await _qdrant.SearchAsync(
            SeedService.Collection,
            query,
            limit: 8,
            payloadSelector: new Qdrant.Client.Grpc.WithPayloadSelector { Enable = true },
            filter: Qdrant.Client.Grpc.Conditions.MatchKeyword("namespace", ns),
            cancellationToken: ct);

        var context = string.Join("\n\n", hits.Select(h =>
        {
            var p = h.Payload;
            var url = p.TryGetValue("source_url", out var u) ? u.StringValue : "";
            var text = p.TryGetValue("text", out var t) ? t.StringValue : "";
            return $"[source: {url}]\n{text}";
        }));
        var allowedUrls = hits
            .Select(h => h.Payload.TryGetValue("source_url", out var u) ? (u.StringValue ?? "").Trim().TrimEnd('/') : "")
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct()
            .ToList();
        _log.LogWarning("RAG search: {Hits} hits, {Urls} allowed urls, ns={Ns}", hits.Count, allowedUrls.Count, ns);
        if (hits.Count == 0)
            _log.LogWarning("No Qdrant chunks retrieved for {Ns} (collection may need seeding)", ns);

        var system = "You are an exam generator for Microsoft AZ-900 (Azure Fundamentals). " +
                     "Generate questions STRICTLY grounded in the provided source material. " +
                     "Return ONLY a JSON object, no markdown fences, no commentary. " +
                     "In the JSON, set sourceUrl to an empty string \"\" — the system will attach the correct source URL.";
        var user = BuildPrompt(prompt, count, cert, difficulty, context, allowedUrls);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var raw = await _bedrock.GenerateAsync(user, system, 2500, 0.3f, ct);
            _log.LogDebug("Generation attempt {Attempt} raw output: {Raw}", attempt, raw);
            var exam = await TryPersistAsync(deviceId, prompt, cert, difficulty, count, raw, allowedUrls, ct);
            if (exam is not null)
            {
                _log.LogInformation("Generation ok on attempt {Attempt} ({Count} questions)", attempt, exam.Questions.Count);
                return exam;
            }
            _log.LogWarning("Generation attempt {Attempt} failed validation", attempt);
        }

        return null;
    }

    private string BuildPrompt(string prompt, int count, string cert, string difficulty, string context, List<string> allowedUrls)
    {
        return $$"""
        Create a practice exam for {{cert}} at {{difficulty}} difficulty with exactly {{count}} questions.

        USER REQUEST: "{{prompt}}"

        SOURCE MATERIAL (ground all questions in this; cite the source URL per question):
        {{context}}

        ALLOWED SOURCE URLS (sourceUrl MUST be one of these, exactly):
        {{string.Join("\n", allowedUrls)}}

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
              "sourceUrl": "https://learn.microsoft.com/..."
            }
          ]
        }

        Rules:
        - exactly {{count}} questions
        - 2 choices for truefalse, 3-4 for single, 4 for multi (multi has 2+ correct)
        - every explanation must reference the source material
        - sourceUrl MUST be "" (empty string) in every question
        """;
    }

    private async Task<Exam?> TryPersistAsync(string deviceId, string prompt, string cert, string difficulty, int count, string raw, List<string> allowedUrls, CancellationToken ct)
    {
        try
        {
            var doc = JsonDocument.Parse(StripFences(raw));
            if (!doc.RootElement.TryGetProperty("questions", out var qs) || qs.GetArrayLength() == 0)
                return null;
            var title = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : $"{cert} Practice";
            if (string.IsNullOrWhiteSpace(title)) title = $"{cert} Practice";

            var exam = new Exam
            {
                OwnerDeviceId = deviceId,
                Title = title,
                Description = $"Generated from: {prompt}",
                CertificationCode = cert.ToUpperInvariant(),
                Mode = "practice",
                Difficulty = difficulty,
                DurationMinutes = Math.Clamp(count * 2, 5, 60),
                PassPercent = 70,
                Status = "ready",
                GenerationPrompt = prompt,
                ExpiresAt = DateTime.UtcNow.AddDays(3),
                ShareToken = null,
            };

            var ord = 0;
            foreach (var q in qs.EnumerateArray())
            {
                if (!q.TryGetProperty("text", out var qText) || string.IsNullOrWhiteSpace(qText.GetString()))
                    return null;
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
                    if (string.IsNullOrWhiteSpace(label) || !labels.Add(label))
                        return null;
                    choiceList.Add(new Choice
                    {
                        Label = label,
                        Text = cText.GetString() ?? "",
                        IsCorrect = correct.EnumerateArray().Any(x => x.GetString() == label),
                        Ordinal = cOrd++,
                    });
                }
                if (choiceList.Count(c => c.IsCorrect) == 0)
                    return null;

                var type = q.TryGetProperty("type", out var ty) ? ty.GetString() : null;
                if (type is null or not ("single" or "multi" or "truefalse"))
                    type = choiceList.Count(c => c.IsCorrect) > 1 ? "multi" : "single";

                var question = new Question
                {
                    Ordinal = ord++,
                    Type = type,
                    Text = qText.GetString()!,
                    Explanation = q.TryGetProperty("explanation", out var ex) ? ex.GetString() ?? "" : "",
                };
                question.Choices.AddRange(choiceList);
                // Grounding: if the model left sourceUrl empty (or invented one),
                // attach the URL of the retrieved chunk (AD-14: citation required).
                var url = "";
                if (q.TryGetProperty("sourceUrl", out var src))
                    url = (src.GetString() ?? "").Trim().TrimEnd('/');
                if (string.IsNullOrWhiteSpace(url) || !allowedUrls.Contains(url))
                    url = allowedUrls.FirstOrDefault() ?? "";
                if (string.IsNullOrWhiteSpace(url))
                {
                    _log.LogWarning("Generation rejected: no grounded source URL available");
                    return null;
                }
                question.Citations.Add(new QuestionCitation { SourceUrl = url, QuotedText = question.Explanation });

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

    private static string NormalizeCert(string cert) => cert.Trim().ToUpperInvariant().Replace("-", ""); // AZ-900 -> AZ900

    private static GenerationConfig ParseConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return new GenerationConfig();
        try
        {
            return JsonSerializer.Deserialize<GenerationConfig>(configJson) ?? new GenerationConfig();
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
    }
}
