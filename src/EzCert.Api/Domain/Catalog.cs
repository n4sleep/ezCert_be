namespace EzCert.Api.Domain;

// Catalog hierarchy: Certification -> Exam -> ExamSection -> QuestionPool -> Question -> Choice
// Mirrors phase1-plan.md Section 2.4 core data model.

public class Certification
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;   // e.g. "AZ-900"
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Exam> Exams { get; set; } = new();
    public List<ProcessedChunk> Chunks { get; set; } = new();
}

public class Exam
{
    public Guid Id { get; set; }
    public Guid CertificationId { get; set; }
    public Certification? Certification { get; set; }

    public string Name { get; set; } = string.Empty;
    public int PassPercent { get; set; } = 70;
    public int? TimeLimitMinutes { get; set; }

    public List<ExamSection> Sections { get; set; } = new();
}

public class ExamSection
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public string Slug { get; set; } = string.Empty;   // e.g. "cloud-computing"
    public string Name { get; set; } = string.Empty;
    public int Ordinal { get; set; }

    public List<QuestionPool> Pools { get; set; } = new();
}

public class QuestionPool
{
    public Guid Id { get; set; }
    public Guid ExamSectionId { get; set; }
    public ExamSection? ExamSection { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<Question> Questions { get; set; } = new();
}

public class Question
{
    public Guid Id { get; set; }
    public Guid QuestionPoolId { get; set; }
    public QuestionPool? QuestionPool { get; set; }

    public string ExternalId { get; set; } = string.Empty;  // e.g. "c1"
    public QuestionType Type { get; set; }
    public Difficulty Difficulty { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string? Source { get; set; }

    public List<Choice> Choices { get; set; } = new();
}

public class Choice
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }

    public string Label { get; set; } = string.Empty;   // e.g. "a"
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int Ordinal { get; set; }
}

// RAG: cleaned + chunked source content, one row per chunk, optionally embedded into Qdrant.
public class ProcessedChunk
{
    public Guid Id { get; set; }
    public Guid CertificationId { get; set; }
    public Certification? Certification { get; set; }

    public string SectionSlug { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public string? VectorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
