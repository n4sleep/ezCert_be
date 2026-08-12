using EzCert.Processor.Features.Exams;
using EzCert.Processor.Features.Generation;
using EzCert.Processor.Features.Guests;
using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Processor.Features.Generation;

// POST /api/exam-jobs  { prompt, config? } -> jobId   (AD-7: async generation)
// GET  /api/exam-jobs/{id} -> { status, examId?, error? }
// For the vertical slice, generation is synchronous-deterministic from the
// bundled mini bank; the worker/queue + Bedrock wiring lands next.
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

            // Vertical-slice generation: build a 5-question exam synchronously
            // from the bundled mini bank, then mark the job complete.
            var exam = MiniBank.BuildExam(job.OwnerDeviceId ?? "", req.Prompt);
            db.Exams.Add(exam);
            await db.SaveChangesAsync();

            job.ExamId = exam.Id;
            job.Status = "completed";
            job.Progress = 1;
            await db.SaveChangesAsync();

            return Results.Created($"/api/exam-jobs/{job.Id}", new { jobId = job.Id });
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
