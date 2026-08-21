using EzCert.Processor.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace EzCert.Processor.Features.Generation;

// DB-backed generation worker (WS-3A): claims queued ProcessingJobs atomically
// from PostgreSQL (the durable queue), executes the generation pipeline with
// stage updates, and never holds work in memory. A single in-process instance
// polls; JobQueue is only a wake-up optimization.
public class ProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobQueue _queue;
    private readonly ILogger<ProcessingWorker> _log;

    public ProcessingWorker(IServiceScopeFactory scopeFactory, JobQueue queue, ILogger<ProcessingWorker> log)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReclaimOrphanedAsync(stoppingToken);
        _log.LogInformation("ProcessingWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid? jobId = null;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EzCertDbContext>();
                jobId = await TryClaimAsync(db, stoppingToken);
            }

            if (jobId is Guid id)
            {
                await ProcessJobAsync(id, stoppingToken);
            }
            else
            {
                await _queue.WaitAsync(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    // Atomic claim: the row only moves queued -> running when still queued.
    // ExecuteUpdateAsync returns the affected row count; a concurrent worker
    // racing for the same row gets 0 and yields.
    private static async Task<Guid?> TryClaimAsync(EzCertDbContext db, CancellationToken ct)
    {
        var candidate = await db.ProcessingJobs
            .Where(j => j.Status == "queued")
            .OrderBy(j => j.CreatedAt)
            .Select(j => j.Id)
            .FirstOrDefaultAsync(ct);
        if (candidate == Guid.Empty) return null;

        var updated = await db.ProcessingJobs
            .Where(j => j.Id == candidate && j.Status == "queued")
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, "running")
                .SetProperty(j => j.Stage, "researching")
                .SetProperty(j => j.ClaimedAt, DateTime.UtcNow)
                .SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);
        return updated == 1 ? candidate : null;
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EzCertDbContext>();
        var generation = scope.ServiceProvider.GetRequiredService<GenerationService>();
        var discovery = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var job = await db.ProcessingJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null) return;

        try
        {
            job.Stage = "researching";
            job.Progress = 0.1;
            await db.SaveChangesAsync(ct);

            var cfg = GetJobConfig(job.ConfigJson);
            job.Stage = "searching";
            await db.SaveChangesAsync(ct);
            var namespaces = await discovery.ResolveNamespacesAsync(job.Prompt, cfg.Cert, cfg.SourceIds, cfg.AutoCrawl, ct);
            if (namespaces.Count == 0)
            {
                job.Status = "failed";
                job.Stage = "failed";
                job.Error = DiscoveryService.IsCertTopic(job.Prompt, cfg.Cert)
                    ? "Couldn't retrieve source material for this certification — try again shortly."
                    : "Couldn't find enough trustworthy source material for this topic. Try a more specific topic or a known certification (e.g. AZ-900, CLF-C02).";
                job.Progress = 1;
                await db.SaveChangesAsync(ct);
                _log.LogWarning("Job {JobId} failed: no namespaces resolved", jobId);
                return;
            }

            var exam = await generation.GenerateAsync(job.OwnerDeviceId ?? "", job.Prompt, job.ConfigJson, namespaces,
                stage => UpdateStageAsync(db, job, stage), ct);

            if (exam is null)
            {
                job.Status = "failed";
                job.Stage = "failed";
                job.Error = DiscoveryService.IsCertTopic(job.Prompt, cfg.Cert)
                    ? "Generation failed after retries — the AI service is unavailable or returned invalid content. Try again shortly."
                    : "Couldn't generate a grounded exam from the discovered material — the retrieved sources were insufficient or the AI service was unavailable. Try again.";
                job.Progress = 1;
            }
            else
            {
                // Cancelled while generating? Discard the exam so it never surfaces.
                await db.Entry(job).ReloadAsync(ct);
                if (job.Status == "cancelled")
                {
                    db.Exams.Remove(exam);
                    job.Progress = 1;
                    await db.SaveChangesAsync(ct);
                    _log.LogInformation("Job {JobId} cancelled — generated exam discarded", jobId);
                    return;
                }
                job.ExamId = exam.Id;
                job.Status = "completed";
                job.Stage = "completed";
                job.Progress = 1;
                job.Error = null;
            }
            await db.SaveChangesAsync(ct);
            _log.LogInformation("Job {JobId} -> {Status} (exam {ExamId})", jobId, job.Status, job.ExamId);
        }
        catch (OperationCanceledException)
        {
            job.Status = "failed";
            job.Stage = "failed";
            job.Error = "Cancelled";
            job.Progress = 1;
            await db.SaveChangesAsync(CancellationToken.None);
            _log.LogWarning("Job {JobId} cancelled", jobId);
        }
        catch (Exception ex)
        {
            job.Status = "failed";
            job.Stage = "failed";
            job.Error = "Generation failed — the AI service is unavailable right now. Please try again.";
            job.Progress = 1;
            await db.SaveChangesAsync(CancellationToken.None);
            _log.LogError(ex, "Job {JobId} failed", jobId);
        }
    }

    private sealed record JobConfig(string? Cert, List<string>? SourceIds, bool AutoCrawl);

    private static JobConfig GetJobConfig(string configJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(configJson);
            var root = doc.RootElement;
            var cert = root.TryGetProperty("cert", out var c) ? c.GetString() : null;
            var sourceIds = new List<string>();
            if (root.TryGetProperty("sourceIds", out var sids) && sids.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var s in sids.EnumerateArray())
                {
                    var v = s.GetString();
                    if (!string.IsNullOrWhiteSpace(v)) sourceIds.Add(v);
                }
            }
            var autoCrawl = !root.TryGetProperty("autoCrawl", out var ac) || ac.ValueKind != System.Text.Json.JsonValueKind.False;
            return new JobConfig(cert, sourceIds, autoCrawl);
        }
        catch (System.Text.Json.JsonException)
        {
            return new JobConfig(null, null, true);
        }
    }

    private static async Task UpdateStageAsync(EzCertDbContext db, ProcessingJob job, string stage)
    {
        job.Stage = stage;
        job.Progress = stage switch
        {
            "embedding" => 0.3,
            "generating" => 0.6,
            "validating" => 0.8,
            "persisting" => 0.9,
            _ => job.Progress,
        };
        job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // On boot, anything still 'running' died with the previous process.
    private async Task ReclaimOrphanedAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EzCertDbContext>();
        var n = await db.ProcessingJobs
            .Where(j => j.Status == "running")
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, "queued")
                .SetProperty(j => j.Stage, "queued")
                .SetProperty(j => j.ClaimedAt, (DateTime?)null)
                .SetProperty(j => j.Error, "reclaimed after restart")
                .SetProperty(j => j.UpdatedAt, DateTime.UtcNow), ct);
        if (n > 0) _log.LogWarning("Reclaimed {Count} orphaned running jobs", n);
    }
}
