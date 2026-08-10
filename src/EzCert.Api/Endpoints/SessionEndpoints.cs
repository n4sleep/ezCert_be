using System.Text.Json;
using EzCert.Api.Contracts;
using EzCert.Api.Data;
using EzCert.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Api.Endpoints;

// Exam Session persistence + scoring (Story 3.6).
public static class SessionEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions").WithTags("Sessions");
        // Start a session: snapshot questions (immutable), randomize order for certification mode.
        group.MapPost("/", async (StartSessionRequest req, EzCertDbContext db, CancellationToken ct) =>
        {
            var mode = Enum.TryParse<ExamMode>(req.Mode, true, out var m) ? m : ExamMode.Practice;

            var exam = await db.Exams
                .Include(e => e.Sections).ThenInclude(s => s.Pools).ThenInclude(p => p.Questions).ThenInclude(q => q.Choices)
                .FirstOrDefaultAsync(e => e.Id == req.ExamId, ct);
            if (exam is null) return Results.NotFound(new { error = "Exam not found." });

            var wantedSlugs = req.SectionSlugs is { Count: > 0 }
                ? new HashSet<string>(req.SectionSlugs, StringComparer.OrdinalIgnoreCase)
                : null;

            var picked = new List<(Question q, ExamSection sec)>();
            foreach (var sec in exam.Sections.OrderBy(s => s.Ordinal))
            {
                if (wantedSlugs is not null && !wantedSlugs.Contains(sec.Slug)) continue;
                foreach (var pool in sec.Pools)
                    foreach (var q in pool.Questions)
                        picked.Add((q, sec));
            }

            picked = mode == ExamMode.Certification
                ? picked.OrderBy(_ => Random.Shared.Next()).ToList()
                : picked.OrderBy(t => t.sec.Ordinal).ThenBy(t => t.q.ExternalId).ToList();

            if (req.QuestionCount is > 0 && req.QuestionCount.Value < picked.Count)
                picked = picked.Take(req.QuestionCount.Value).ToList();

            if (picked.Count == 0)
                return Results.BadRequest(new { error = "No questions match the requested sections." });

            var session = new ExamSession
            {
                ExamId = exam.Id,
                UserRef = string.IsNullOrWhiteSpace(req.UserRef) ? "demo-user" : req.UserRef!,
                Mode = mode,
                Status = SessionStatus.InProgress,
                StartedAt = DateTime.UtcNow
            };
            if (mode == ExamMode.Certification && exam.TimeLimitMinutes is { } mins)
                session.ExpiresAt = session.StartedAt.AddMinutes(mins);

            var ord = 0;
            foreach (var (q, sec) in picked)
            {
                var choices = q.Choices.OrderBy(c => c.Ordinal).Select(c => new SessionChoiceDto(c.Label, c.Text)).ToList();
                var correct = q.Choices.Where(c => c.IsCorrect).Select(c => c.Label).ToList();
                session.Snapshots.Add(new QuestionSnapshot
                {
                    QuestionId = q.Id,
                    Ordinal = ord++,
                    SectionSlug = sec.Slug,
                    SectionName = sec.Name,
                    Type = q.Type.ToString(),
                    Text = q.Text,
                    Explanation = q.Explanation,
                    Source = q.Source,
                    ChoicesJson = JsonSerializer.Serialize(choices, Json),
                    CorrectJson = JsonSerializer.Serialize(correct, Json)
                });
            }

            db.ExamSessions.Add(session);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/sessions/{session.Id}", ToSessionDto(session));
        });

        // Get current session state (no correct answers revealed).
        group.MapGet("/{id:guid}", async (Guid id, EzCertDbContext db, CancellationToken ct) =>
        {
            var session = await db.ExamSessions
                .Include(s => s.Snapshots)
                .FirstOrDefaultAsync(s => s.Id == id, ct);
            if (session is null) return Results.NotFound();

            if (EnsureExpiry(session)) await db.SaveChangesAsync(ct);
            return Results.Ok(ToSessionDto(session));
        });

        // Submit one answer. Practice mode reveals correctness + explanation; certification records silently.
        group.MapPost("/{id:guid}/answers", async (Guid id, SubmitAnswerRequest req, EzCertDbContext db, CancellationToken ct) =>
        {
            var session = await db.ExamSessions
                .Include(s => s.Snapshots).ThenInclude(sn => sn.Answers)
                .FirstOrDefaultAsync(s => s.Id == id, ct);
            if (session is null) return Results.NotFound();

            if (EnsureExpiry(session))
            {
                await db.SaveChangesAsync(ct);
                return Results.Conflict(new { error = "Session has expired." });
            }
            if (session.Status != SessionStatus.InProgress)
                return Results.Conflict(new { error = $"Session is {session.Status}." });

            var snap = session.Snapshots.FirstOrDefault(s => s.Id == req.SnapshotId);
            if (snap is null) return Results.NotFound(new { error = "Question snapshot not found in this session." });

            var selected = (req.Selected ?? new List<string>()).ToList();
            var correct = DeserList(snap.CorrectJson);
            var isCorrect = IsCorrect(selected, correct);

            var existing = snap.Answers.FirstOrDefault();
            if (existing is null)
            {
                snap.Answers.Add(new AnswerSubmission
                {
                    SelectedJson = JsonSerializer.Serialize(selected, Json),
                    IsCorrect = isCorrect,
                    SubmittedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.SelectedJson = JsonSerializer.Serialize(selected, Json);
                existing.IsCorrect = isCorrect;
                existing.SubmittedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);

            if (session.Mode == ExamMode.Practice)
                return Results.Ok(new AnswerResultDto(snap.Id, true, isCorrect, correct, snap.Explanation, snap.Source));

            return Results.Ok(new AnswerResultDto(snap.Id, true, null, null, null, null));
        });

        // Finalize and score the session. Checks expiry first so an expired
        // certification session is finalized from answers recorded before the
        // deadline. Idempotent: returns the existing report if already submitted.
        group.MapPost("/{id:guid}/submit", async (Guid id, EzCertDbContext db, CancellationToken ct) =>
        {
            var session = await db.ExamSessions
                .Include(s => s.Exam)
                .Include(s => s.Snapshots).ThenInclude(sn => sn.Answers)
                .Include(s => s.ScoreReport).ThenInclude(r => r!.SectionScores)
                .FirstOrDefaultAsync(s => s.Id == id, ct);
            if (session is null) return Results.NotFound();

            // AC1: server-authoritative timer — recognize expiry on submit too, not
            // just on status/answers. Only transitions an InProgress session; the
            // final SaveChangesAsync below persists the transition.
            EnsureExpiry(session);

            var passPercent = session.Exam?.PassPercent ?? 70;

            if (session.ScoreReport is not null)
                return Results.Ok(BuildReportDto(session, passPercent));

            var total = session.Snapshots.Count;
            var correctCount = session.Snapshots.Count(sn => sn.Answers.FirstOrDefault()?.IsCorrect == true);
            var percent = total > 0 ? Math.Round(correctCount * 100.0 / total, 1) : 0.0;
            var passed = percent >= passPercent;

            var report = new ScoreReport
            {
                ExamSessionId = session.Id,
                TotalQuestions = total,
                CorrectCount = correctCount,
                ScorePercent = percent,
                Passed = passed,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var grp in session.Snapshots.GroupBy(sn => new { sn.SectionSlug, sn.SectionName }))
            {
                var secTotal = grp.Count();
                var secCorrect = grp.Count(sn => sn.Answers.FirstOrDefault()?.IsCorrect == true);
                report.SectionScores.Add(new SectionScore
                {
                    SectionSlug = grp.Key.SectionSlug,
                    SectionName = grp.Key.SectionName,
                    Total = secTotal,
                    Correct = secCorrect,
                    Percent = secTotal > 0 ? Math.Round(secCorrect * 100.0 / secTotal, 1) : 0.0
                });
            }

            session.ScoreReport = report;
            session.Status = SessionStatus.Submitted;
            session.SubmittedAt = DateTime.UtcNow;

            if (passed && session.Mode == ExamMode.Certification && session.Exam is not null)
            {
                db.Credentials.Add(new Credential
                {
                    UserRef = session.UserRef,
                    CertificationId = session.Exam.CertificationId
                });
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(BuildReportDto(session, passPercent));
        });

        // Fetch a previously computed score report.
        group.MapGet("/{id:guid}/results", async (Guid id, EzCertDbContext db, CancellationToken ct) =>
        {
            var session = await db.ExamSessions
                .Include(s => s.Exam)
                .Include(s => s.Snapshots).ThenInclude(sn => sn.Answers)
                .Include(s => s.ScoreReport).ThenInclude(r => r!.SectionScores)
                .FirstOrDefaultAsync(s => s.Id == id, ct);
            if (session is null) return Results.NotFound();
            if (session.ScoreReport is null) return Results.NotFound(new { error = "Session has not been submitted." });

            return Results.Ok(BuildReportDto(session, session.Exam?.PassPercent ?? 70));
        });

        return app;
    }

    // ---- helpers ----
    private static List<string> DeserList(string json)
        => JsonSerializer.Deserialize<List<string>>(json, Json) ?? new List<string>();

    private static bool IsCorrect(IEnumerable<string> selected, IEnumerable<string> correct)
    {
        var a = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
        var b = new HashSet<string>(correct, StringComparer.OrdinalIgnoreCase);
        return a.SetEquals(b);
    }

    private static bool EnsureExpiry(ExamSession s)
    {
        if (s.Status == SessionStatus.InProgress && s.ExpiresAt is { } exp && exp < DateTime.UtcNow)
        {
            s.Status = SessionStatus.Expired;
            return true;
        }
        return false;
    }

    private static SessionDto ToSessionDto(ExamSession s) => new(
        s.Id, s.ExamId, s.Mode.ToString(), s.Status.ToString(), s.StartedAt, s.ExpiresAt,
        s.Snapshots.OrderBy(q => q.Ordinal).Select(q => new SessionQuestionDto(
            q.Id, q.Ordinal, q.SectionSlug, q.SectionName, q.Type, q.Text,
            (JsonSerializer.Deserialize<List<SessionChoiceDto>>(q.ChoicesJson, Json) ?? new())
        )).ToList());

    private static ScoreReportDto BuildReportDto(ExamSession s, int passPercent)
    {
        var r = s.ScoreReport!;
        var sections = r.SectionScores
            .Select(ss => new SectionScoreDto(ss.SectionSlug, ss.SectionName, ss.Total, ss.Correct, ss.Percent))
            .ToList();

        var review = s.Snapshots.OrderBy(sn => sn.Ordinal).Select(sn =>
        {
            var answer = sn.Answers.FirstOrDefault();
            var selected = answer is null ? new List<string>() : DeserList(answer.SelectedJson);
            var correct = DeserList(sn.CorrectJson);
            return new ReviewItemDto(
                sn.Ordinal, sn.Text, selected, correct,
                answer?.IsCorrect ?? false, sn.Explanation, sn.Source);
        }).ToList();

        return new ScoreReportDto(
            s.Id, r.TotalQuestions, r.CorrectCount, r.ScorePercent, r.Passed, passPercent, sections, review);
    }
}
