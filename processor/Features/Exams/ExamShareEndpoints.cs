using EzCert.Processor.Features.Guests;
using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Processor.Features.Exams;

// Share-by-link (AD-4): POST /api/exams/{id}/share issues a unique share_token;
// GET /api/exams/take/{shareToken} resolves it while the exam is alive;
// DELETE /api/exams/{id} (owner device) invalidates it.
public static class ExamShareEndpoints
{
    public static IEndpointRouteBuilder MapExamShareEndpoints(this IEndpointRouteBuilder app)
    {
        // List this device's exams (persisted history, newest first) — powers the
        // Exam tab + ChatGPT-style sidebar.
        app.MapGet("/api/exams", async (EzCertDbContext db, HttpContext ctx) =>
        {
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            var now = DateTime.UtcNow;
            var rows = await db.Exams
                .Where(e => e.OwnerDeviceId == device)
                .OrderByDescending(e => e.CreatedAt)
                .Take(100)
                .Select(e => new
                {
                    examId = e.Id,
                    title = e.Title,
                    mode = e.Mode,
                    difficulty = e.Difficulty,
                    status = e.Status,
                    // Computed lifecycle (WS-6): status values stay as-is; expiry
                    // is derived from ExpiresAt. Archived = reserved for future.
                    expired = e.Status == "ready" && e.ExpiresAt < now,
                    questionCount = e.Questions.Count,
                    durationMinutes = e.DurationMinutes,
                    expiresAt = e.ExpiresAt,
                    createdAt = e.CreatedAt,
                })
                .ToListAsync();
            return Results.Ok(rows);
        });

        app.MapPost("/api/exams/{examId:guid}/share", async (Guid examId, EzCertDbContext db, HttpContext ctx) =>
        {
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == examId);
            if (exam is null) return Results.NotFound();
            if (exam.OwnerDeviceId != device) return Results.NotFound(); // only owner shares
            if (exam.Status != "ready") return Results.Conflict(new { error = $"Exam is {exam.Status}." });
            if (exam.ExpiresAt < DateTime.UtcNow) return Results.Conflict(new { error = "This exam has expired." });

            exam.ShareToken ??= Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();

            // The public SPA origin comes from config — the request host is the API
            // (App Runner terminates TLS, so Request.Scheme is unreliable here).
            var config = ctx.RequestServices.GetRequiredService<IConfiguration>();
            var siteUrl = config["PublicSiteUrl"] ?? "http://localhost:5173";
            return Results.Ok(new { shareToken = exam.ShareToken, url = $"{siteUrl.TrimEnd('/')}/?take={exam.ShareToken}" });
        });

        app.MapGet("/api/exams/take/{shareToken}", async (string shareToken, EzCertDbContext db, HttpContext ctx) =>
        {
            var exam = await db.Exams
                .Include(e => e.Questions).ThenInclude(q => q.Choices)
                .FirstOrDefaultAsync(e => e.ShareToken == shareToken);
            if (exam is null) return Results.NotFound();
            if (exam.Status != "ready") return Results.Conflict(new { error = $"Exam is {exam.Status}." });
            if (exam.ExpiresAt < DateTime.UtcNow) return Results.Conflict(new { error = "This exam has expired." });

            return Results.Ok(new
            {
                examId = exam.Id,
                title = exam.Title,
                description = exam.Description,
                mode = exam.Mode,
                questionCount = exam.Questions.Count,
                durationMinutes = exam.DurationMinutes,
                expiresAt = exam.ExpiresAt,
            });
        });

        app.MapDelete("/api/exams/{examId:guid}", async (Guid examId, EzCertDbContext db, HttpContext ctx) =>
        {
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            var exam = await db.Exams.FirstOrDefaultAsync(e => e.Id == examId);
            if (exam is null) return Results.NotFound();
            if (exam.OwnerDeviceId != device) return Results.NotFound(); // only owner deletes

            exam.Status = "archived";
            exam.ShareToken = null; // invalidate the link
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        return app;
    }
}
