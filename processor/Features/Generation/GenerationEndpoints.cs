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
        app.MapPost("/api/exam-jobs", async (CreateJobRequest req, EzCertDbContext db, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Prompt))
                return Results.BadRequest(new { error = "prompt is required" });

            var job = new ProcessingJob
            {
                OwnerDeviceId = GuestIdentity.GetOrCreateDeviceId(ctx),
                Prompt = req.Prompt,
                ConfigJson = req.ConfigJson ?? "{}",
                Status = "queued",
            };
            db.ProcessingJobs.Add(job);
            await db.SaveChangesAsync();

            // Inline execution for the demo (job table stays the queue contract
            // for a later background worker, AD-7).
            var generation = ctx.RequestServices.GetRequiredService<GenerationService>();
            job.Status = "running";
            job.Progress = 0.2;
            await db.SaveChangesAsync();

            try
            {
                var exam = await generation.GenerateAsync(job.OwnerDeviceId ?? "", req.Prompt, req.ConfigJson, ctx.RequestAborted);
                if (exam is null)
                {
                    job.Status = "failed";
                    job.Error = "Generation failed after retries — Bedrock is unavailable or returned invalid content. Try again shortly.";
                    job.Progress = 1;
                    await db.SaveChangesAsync();
                    return Results.Accepted($"/api/exam-jobs/{job.Id}", new { jobId = job.Id });
                }
                job.ExamId = exam.Id;
                job.Status = "completed";
                job.Progress = 1;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GenerationEndpoints");
                log.LogError(ex, "Exam job {JobId} failed", job.Id);
                job.Status = "failed";
                job.Error = "Generation failed — the AI service is unavailable right now. Please try again.";
                job.Progress = 1;
                await db.SaveChangesAsync();
            }

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
                examId = job.ExamId,
                error = job.Error,
                progress = job.Progress,
            });
        });

        return app;
    }
}
