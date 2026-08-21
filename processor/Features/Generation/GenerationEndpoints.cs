using EzCert.Processor.Features.Exams;
using EzCert.Processor.Features.Generation;
using EzCert.Processor.Features.Guests;
using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Processor.Features.Generation;

// POST /api/exam-jobs  { prompt, config? } -> jobId   (AD-7: async generation)
// GET  /api/exam-jobs/{id} -> { status, examId?, error? }
// Generation runs through the real pipeline: Qdrant retrieval + Bedrock via
// the gateway, with validation+retry (<=3). AI failure degrades to a failed
// job (AD-7: 503-class behavior on the AI path; official bank untouched).
public static class GenerationEndpoints
{
    public record CreateJobRequest(string Prompt, string? ConfigJson);

    public static IEndpointRouteBuilder MapGenerationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/exam-jobs", async (CreateJobRequest req, EzCertDbContext db, HttpContext ctx, JobQueue queue) =>
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
                return Results.BadRequest(new { error = "prompt is required" });

            var job = new ProcessingJob
            {
                OwnerDeviceId = GuestIdentity.GetOrCreateDeviceId(ctx),
                Prompt = req.Prompt,
                ConfigJson = req.ConfigJson ?? "{}",
                Status = "queued",
                Stage = "queued",
            };
            db.ProcessingJobs.Add(job);
            await db.SaveChangesAsync();

            // WS-3A: enqueue only — the ProcessingWorker executes the pipeline
            // from the DB queue; the 202 returns immediately. JobQueue is just
            // a wake-up signal so the poll loop is not delayed.
            queue.Wake(job.Id);

            return Results.Accepted($"/api/exam-jobs/{job.Id}", new { jobId = job.Id });
        });

        app.MapGet("/api/exam-jobs/{id:guid}", async (Guid id, EzCertDbContext db, HttpContext ctx) =>
        {
            var job = await db.ProcessingJobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job is null) return Results.NotFound();
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            if (job.OwnerDeviceId != device) return Results.NotFound();

            return Results.Ok(new
            {
                jobId = job.Id,
                status = job.Status,
                stage = job.Stage,
                examId = job.ExamId,
                error = job.Error,
                progress = job.Progress,
            });
        });

        // Cancel a queued/running job. The worker discards any exam that
        // finishes after the cancel lands (idempotent).
        app.MapPost("/api/exam-jobs/{id:guid}/cancel", async (Guid id, EzCertDbContext db, HttpContext ctx) =>
        {
            var job = await db.ProcessingJobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job is null) return Results.NotFound();
            var device = GuestIdentity.GetOrCreateDeviceId(ctx);
            if (job.OwnerDeviceId != device) return Results.NotFound();

            if (job.Status == "queued" || job.Status == "running")
            {
                job.Status = "cancelled";
                job.Stage = "cancelled";
                job.Progress = 1;
                await db.SaveChangesAsync();
            }
            return Results.Ok(new { jobId = job.Id, status = job.Status });
        });

        return app;
    }
}
