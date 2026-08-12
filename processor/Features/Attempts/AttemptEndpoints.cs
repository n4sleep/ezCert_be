using System.Text.Json;
using EzCert.Processor.Features.Exams;
using EzCert.Processor.Features.Guests;
using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Processor.Features.Attempts;

// Attempt lifecycle (AD-5): start snapshots questions; answers recorded
// server-side; submit scores from snapshots; results owner-only.
public static class AttemptEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public record AnswerRequest(Guid AttemptQuestionId, IReadOnlyList<string> Selected);
    public record AnswerDto(Guid AttemptQuestionId, bool? IsCorrect, string[]? Correct, string? Explanation, string? Source);

    public static IEndpointRouteBuilder MapAttemptEndpoints(this IEndpointRouteBuilder app)
    {
        // Start an attempt on a ready exam.
        app.MapPost("/api/exams/{examId:guid}/attempts", async (Guid examId, EzCertDbContext db, HttpContext ctx) =>
        {
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            var exam = await db.Exams
                .Include(e => e.Questions).ThenInclude(q => q.Choices)
                .Include(e => e.Questions).ThenInclude(q => q.Citations)
                .FirstOrDefaultAsync(e => e.Id == examId);
            if (exam is null) return Results.NotFound();
            if (exam.Status != "ready") return Results.Conflict(new { error = $"Exam is {exam.Status}." });
            if (exam.ExpiresAt < DateTime.UtcNow) return Results.Conflict(new { error = "This exam has expired." });

            var attempt = new Attempt
            {
                ExamId = exam.Id,
                DeviceId = device,
                Status = "in_progress",
                ExpiresAt = exam.Mode == "certification" ? DateTime.UtcNow.AddMinutes(exam.DurationMinutes) : null,
            };

            foreach (var q in exam.Questions.OrderBy(q => q.Ordinal))
            {
                attempt.Questions.Add(new AttemptQuestion
                {
                    SourceQuestionId = q.Id,
                    Ordinal = q.Ordinal,
                    Section = q.Ordinal < 4 ? "cloud-concepts" : "service-types",
                    QuestionJson = JsonSerializer.Serialize(new { type = q.Type, text = q.Text }, Json),
                    ChoicesJson = JsonSerializer.Serialize(q.Choices.OrderBy(c => c.Ordinal)
                        .Select(c => new { label = c.Label, text = c.Text }).ToList(), Json),
                    CorrectJson = JsonSerializer.Serialize(q.Choices.Where(c => c.IsCorrect).Select(c => c.Label).ToList(), Json),
                    Explanation = q.Explanation,
                    CitationJson = JsonSerializer.Serialize(q.Citations.Select(c => c.SourceUrl).ToList(), Json),
                });
            }
            attempt.TotalQuestions = attempt.Questions.Count;

            db.Attempts.Add(attempt);
            await db.SaveChangesAsync();

            return Results.Created($"/api/attempts/{attempt.Id}", new
            {
                attemptId = attempt.Id,
                status = attempt.Status,
                questions = attempt.Questions.OrderBy(q => q.Ordinal).Select(q => new
                {
                    attemptQuestionId = q.Id,
                    ordinal = q.Ordinal,
                    type = JsonDocument.Parse(q.QuestionJson).RootElement.GetProperty("type").GetString(),
                    text = JsonDocument.Parse(q.QuestionJson).RootElement.GetProperty("text").GetString(),
                    choices = JsonSerializer.Deserialize<List<dynamic>>(q.ChoicesJson, Json),
                }),
            });
        });

        // Record one answer. Practice mode reveals correctness; certification records silently.
        app.MapPost("/api/attempts/{attemptId:guid}/answers", async (Guid attemptId, AnswerRequest req, EzCertDbContext db, HttpContext ctx) =>
        {
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            var attempt = await db.Attempts
                .Include(a => a.Questions).ThenInclude(q => q.Answer)
                .FirstOrDefaultAsync(a => a.Id == attemptId);
            if (attempt is null || attempt.DeviceId != device) return Results.NotFound();
            if (attempt.Status != "in_progress") return Results.Conflict(new { error = $"Attempt is {attempt.Status}." });

            var aq = attempt.Questions.FirstOrDefault(q => q.Id == req.AttemptQuestionId);
            if (aq is null) return Results.NotFound(new { error = "Question not in this attempt." });

            var selected = (req.Selected ?? new List<string>()).ToList();
            var correct = JsonSerializer.Deserialize<List<string>>(aq.CorrectJson, Json) ?? new();
            var isCorrect = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase)
                .SetEquals(new HashSet<string>(correct, StringComparer.OrdinalIgnoreCase));

            if (aq.Answer is null)
            {
                aq.Answer = new Answer { SelectedJson = JsonSerializer.Serialize(selected, Json), IsCorrect = isCorrect };
            }
            else
            {
                aq.Answer.SelectedJson = JsonSerializer.Serialize(selected, Json);
                aq.Answer.IsCorrect = isCorrect;
                aq.Answer.AnsweredAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();

            if (attempt.Exam?.Mode == "certification")
                return Results.Ok(new AnswerDto(aq.Id, null, null, null, null));

            return Results.Ok(new AnswerDto(aq.Id, isCorrect, correct.ToArray(), aq.Explanation,
                JsonSerializer.Deserialize<List<string>>(aq.CitationJson, Json)?.FirstOrDefault()));
        });

        // Submit: score from snapshots; expired mid-attempt still auto-scores (AD-5).
        app.MapPost("/api/attempts/{attemptId:guid}/submit", async (Guid attemptId, EzCertDbContext db, HttpContext ctx) =>
        {
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            var attempt = await db.Attempts
                .Include(a => a.Exam)
                .Include(a => a.Questions).ThenInclude(q => q.Answer)
                .FirstOrDefaultAsync(a => a.Id == attemptId);
            if (attempt is null || attempt.DeviceId != device) return Results.NotFound();

            var expired = attempt.ExpiresAt is { } exp && exp < DateTime.UtcNow;
            if (attempt.Status == "in_progress")
            {
                attempt.Status = expired ? "expired" : "submitted";
                attempt.SubmittedAt = DateTime.UtcNow;
            }

            attempt.CorrectCount = attempt.Questions.Count(q => q.Answer?.IsCorrect == true);
            attempt.ScorePercent = attempt.TotalQuestions > 0
                ? Math.Round(attempt.CorrectCount * 100.0 / attempt.TotalQuestions, 1) : 0;
            attempt.Passed = attempt.ScorePercent >= (attempt.Exam?.PassPercent ?? 70);

            attempt.SectionScores.Clear();
            foreach (var grp in attempt.Questions.GroupBy(q => q.Section))
            {
                var secTotal = grp.Count();
                var secCorrect = grp.Count(q => q.Answer?.IsCorrect == true);
                attempt.SectionScores.Add(new SectionScore
                {
                    Section = grp.Key,
                    Total = secTotal,
                    Correct = secCorrect,
                    Percentage = secTotal > 0 ? Math.Round(secCorrect * 100.0 / secTotal, 1) : 0,
                });
            }

            await db.SaveChangesAsync();
            return Results.Ok(BuildReport(attempt, expired));
        });

        // Results: owner-only.
        app.MapGet("/api/attempts/{attemptId:guid}/results", async (Guid attemptId, EzCertDbContext db, HttpContext ctx) =>
        {
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            var attempt = await db.Attempts
                .Include(a => a.Exam)
                .Include(a => a.Questions).ThenInclude(q => q.Answer)
                .Include(a => a.SectionScores)
                .FirstOrDefaultAsync(a => a.Id == attemptId);
            if (attempt is null || attempt.DeviceId != device) return Results.NotFound();
            if (attempt.Status == "in_progress") return Results.Conflict(new { error = "Attempt not submitted yet." });

            return Results.Ok(BuildReport(attempt, attempt.Status == "expired"));
        });

        // Device history (GET /api/me/attempts).
        app.MapGet("/api/me/attempts", async (EzCertDbContext db, HttpContext ctx) =>
        {
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            var rows = await db.Attempts
                .Include(a => a.Exam)
                .Where(a => a.DeviceId == device)
                .OrderByDescending(a => a.StartedAt)
                .Take(50)
                .Select(a => new
                {
                    attemptId = a.Id,
                    examId = a.ExamId,
                    title = a.Exam != null ? a.Exam.Title : "",
                    status = a.Status,
                    scorePercent = a.ScorePercent,
                    passed = a.Passed,
                    startedAt = a.StartedAt,
                })
                .ToListAsync();
            return Results.Ok(rows);
        });

        return app;
    }

    private static object BuildReport(Attempt a, bool expired)
    {
        var review = a.Questions.OrderBy(q => q.Ordinal).Select(q =>
        {
            var qj = JsonDocument.Parse(q.QuestionJson).RootElement;
            var selected = q.Answer is null ? new List<string>() : JsonSerializer.Deserialize<List<string>>(q.Answer.SelectedJson, Json) ?? new();
            var correct = JsonSerializer.Deserialize<List<string>>(q.CorrectJson, Json) ?? new();
            return new
            {
                ordinal = q.Ordinal,
                text = qj.GetProperty("text").GetString(),
                selected,
                correct,
                isCorrect = q.Answer?.IsCorrect ?? false,
                explanation = q.Explanation,
                source = JsonSerializer.Deserialize<List<string>>(q.CitationJson, Json)?.FirstOrDefault(),
            };
        }).ToList();

        return new
        {
            attemptId = a.Id,
            totalQuestions = a.TotalQuestions,
            correctCount = a.CorrectCount,
            scorePercent = a.ScorePercent,
            passed = a.Passed,
            passPercent = a.Exam?.PassPercent ?? 70,
            expired,
            sections = a.SectionScores.Select(s => new { section = s.Section, total = s.Total, correct = s.Correct, percentage = s.Percentage }),
            review,
        };
    }
}
