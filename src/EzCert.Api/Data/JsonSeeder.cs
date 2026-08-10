using System.Text.Json;
using System.Text.Json.Serialization;
using EzCert.Api.Domain;

namespace EzCert.Api.Data;

// Seeds the catalog from every *-questions.json bank shipped in Data/seed/.
// Idempotent per certification code: a bank is skipped only if its own
// certification is already present, so multiple certs coexist and restarts
// never create duplicates.
public static class JsonSeeder
{
    public static async Task SeedAsync(EzCertDbContext db, string contentRoot, ILogger logger, CancellationToken ct = default)
    {
        var seedDir = Path.Combine(contentRoot, "Data", "seed");
        if (!Directory.Exists(seedDir))
        {
            logger.LogWarning("Seed directory not found at {Path}; skipping seed.", seedDir);
            return;
        }

        var files = Directory.GetFiles(seedDir, "*-questions.json")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
        {
            logger.LogWarning("No *-questions.json seed files found in {Path}; skipping seed.", seedDir);
            return;
        }

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in files)
        {
            SeedBank? bank;
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                bank = JsonSerializer.Deserialize<SeedBank>(json, opts);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Seed file {File} could not be parsed; skipping.", Path.GetFileName(file));
                continue;
            }

            if (bank is null || string.IsNullOrWhiteSpace(bank.Certification))
            {
                logger.LogWarning("Seed file {File} is empty or missing a certification code; skipping.", Path.GetFileName(file));
                continue;
            }

            await SeedBankAsync(db, bank, logger, ct);
        }
    }

    private static async Task SeedBankAsync(EzCertDbContext db, SeedBank bank, ILogger logger, CancellationToken ct)
    {
        if (db.Certifications.Any(c => c.Code == bank.Certification))
        {
            logger.LogInformation("Seed skipped for {Code}: certification already present.", bank.Certification);
            return;
        }

        var cert = new Certification
        {
            Code = bank.Certification,
            Name = bank.Title,
            Description = $"Seeded from the bundled {bank.Certification} question bank."
        };

        var exam = new Exam
        {
            Certification = cert,
            Name = $"{bank.Certification} Fundamentals",
            PassPercent = 70,
            TimeLimitMinutes = 30
        };
        cert.Exams.Add(exam);

        var sectionsBySlug = new Dictionary<string, ExamSection>();
        var poolsBySlug = new Dictionary<string, QuestionPool>();
        var ordinal = 0;
        foreach (var s in bank.Sections)
        {
            var section = new ExamSection { Slug = s.Id, Name = s.Name, Ordinal = ordinal++ };
            var pool = new QuestionPool { Name = $"{s.Name} Pool" };
            section.Pools.Add(pool);
            exam.Sections.Add(section);
            sectionsBySlug[s.Id] = section;
            poolsBySlug[s.Id] = pool;
        }

        foreach (var q in bank.Questions)
        {
            if (!poolsBySlug.TryGetValue(q.Section, out var pool))
            {
                logger.LogWarning("Question {Id} references unknown section {Section}; skipped.", q.Id, q.Section);
                continue;
            }

            var question = new Question
            {
                ExternalId = q.Id,
                Type = ParseType(q.Type),
                Difficulty = ParseDifficulty(q.Difficulty),
                Text = q.Text,
                Explanation = q.Explanation,
                Source = q.Source
            };

            var correct = new HashSet<string>(q.Correct, StringComparer.OrdinalIgnoreCase);
            var cOrd = 0;
            foreach (var c in q.Choices)
            {
                question.Choices.Add(new Choice
                {
                    Label = c.Id,
                    Text = c.Text,
                    IsCorrect = correct.Contains(c.Id),
                    Ordinal = cOrd++
                });
            }

            pool.Questions.Add(question);
        }

        db.Certifications.Add(cert);
        var saved = await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded certification {Code} with {Questions} questions ({Rows} rows).",
            cert.Code, bank.Questions.Count, saved);
    }

    private static QuestionType ParseType(string t) => t.Trim().ToLowerInvariant() switch
    {
        "single" => QuestionType.Single,
        "multi" => QuestionType.Multi,
        "truefalse" or "true-false" or "boolean" => QuestionType.TrueFalse,
        _ => QuestionType.Single
    };

    private static Difficulty ParseDifficulty(string d) => d.Trim().ToLowerInvariant() switch
    {
        "easy" => Difficulty.Easy,
        "hard" => Difficulty.Hard,
        _ => Difficulty.Medium
    };

    // ---- Seed file shape (matches ezCert_fe/src/data/az900-questions.json) ----
    private sealed class SeedBank
    {
        public string Certification { get; set; } = "";
        public string Title { get; set; } = "";
        public List<SeedSection> Sections { get; set; } = new();
        public List<SeedQuestion> Questions { get; set; } = new();
    }

    private sealed class SeedSection
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class SeedQuestion
    {
        public string Id { get; set; } = "";
        public string Section { get; set; } = "";
        public string Type { get; set; } = "single";
        public string Difficulty { get; set; } = "medium";
        public string Text { get; set; } = "";
        public List<SeedChoice> Choices { get; set; } = new();
        public List<string> Correct { get; set; } = new();
        public string Explanation { get; set; } = "";
        public string? Source { get; set; }
    }

    private sealed class SeedChoice
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
    }
}
