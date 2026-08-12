namespace EzCert.Processor.Features.Exams;

using EzCert.Processor.Features.Attempts;

// A generated exam (AD-3: immutable once ready; expires after 3 days; AD-4: share_token).
public class Exam
{
    public Guid Id { get; set; }
    public string? OwnerDeviceId { get; set; }       // null = official bank
    public string? ShareToken { get; set; }          // link sharing (AD-4)
    public string? CertificationCode { get; set; }   // e.g. AZ-900
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Mode { get; set; } = "practice";   // practice | certification
    public string Difficulty { get; set; } = "medium";
    public int DurationMinutes { get; set; } = 15;
    public int PassPercent { get; set; } = 70;
    public string Status { get; set; } = "generating"; // generating | ready | archived | failed
    public string? GenerationPrompt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }          // CreatedAt + 3 days

    public List<Question> Questions { get; set; } = new();
    public List<ExamSource> Sources { get; set; } = new();
    public List<Attempt> Attempts { get; set; } = new();
}

public class ExamSource
{
    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }
    public Guid SourceId { get; set; }
}

public class Question
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }
    public int Ordinal { get; set; }
    public string Type { get; set; } = "single";     // single | multi | truefalse
    public string Text { get; set; } = "";
    public string Explanation { get; set; } = "";
    public List<Choice> Choices { get; set; } = new();
    public List<QuestionCitation> Citations { get; set; } = new();
}

public class Choice
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }
    public int Ordinal { get; set; }
    public string Label { get; set; } = "";          // a, b, c, d
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}

public class QuestionCitation
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public string? SourceUrl { get; set; }
    public string? PageNumber { get; set; }
    public string QuotedText { get; set; } = "";
}
