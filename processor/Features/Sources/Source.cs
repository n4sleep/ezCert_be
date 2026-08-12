namespace EzCert.Processor.Features.Sources;

// A content origin: official crawl, user upload, or crawled URL (AD-6/AD-9).
public class Source
{
    public Guid Id { get; set; }
    public string? OwnerDeviceId { get; set; }      // null = official/system
    public string Kind { get; set; } = "upload";    // official | upload | url
    public string Title { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending | ready | failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SourceDocument> Documents { get; set; } = new();
}

// One normalized artifact (markdown/raw) inside a source.
public class SourceDocument
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public Source? Source { get; set; }
    public string? CanonicalUrl { get; set; }
    public string ObjectUri { get; set; } = "";     // object-storage key
    public string ContentHash { get; set; } = "";
    public DateTime? FetchedAt { get; set; }
}
