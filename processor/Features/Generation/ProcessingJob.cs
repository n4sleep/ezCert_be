namespace EzCert.Processor.Features.Generation;

// Async generation job — the queue (AD-7). Powers the chat flow.
public class ProcessingJob
{
    public Guid Id { get; set; }
    public string? OwnerDeviceId { get; set; }
    public string Kind { get; set; } = "generate";    // crawl | ingest | generate
    public string Status { get; set; } = "queued";    // queued | running | completed | failed
    public string Stage { get; set; } = "queued";     // queued | researching | crawling | embedding | generating | validating | persisting | completed | failed
    public string Prompt { get; set; } = "";
    public string ConfigJson { get; set; } = "{}";    // { cert, mode, difficulty, count, sources[] }
    public Guid? ExamId { get; set; }
    public double? Progress { get; set; }
    public string? Error { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
