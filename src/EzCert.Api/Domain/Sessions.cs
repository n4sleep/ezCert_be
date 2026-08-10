namespace EzCert.Api.Domain;

// Session + scoring + credential entities.
// Snapshots decouple a live session from later question-pool changes.

public class ExamSession
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public string UserRef { get; set; } = "demo-user";
    public ExamMode Mode { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.InProgress;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public List<QuestionSnapshot> Snapshots { get; set; } = new();
    public ScoreReport? ScoreReport { get; set; }
}

public class QuestionSnapshot
{
    public Guid Id { get; set; }
    public Guid ExamSessionId { get; set; }
    public ExamSession? ExamSession { get; set; }

    public Guid QuestionId { get; set; }
    public int Ordinal { get; set; }

    public string SectionSlug { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string? Source { get; set; }

    // Immutable JSON copies captured at session start.
    public string ChoicesJson { get; set; } = "[]";   // [{label,text}]
    public string CorrectJson { get; set; } = "[]";    // ["a","b"]

    public List<AnswerSubmission> Answers { get; set; } = new();
}

public class AnswerSubmission
{
    public Guid Id { get; set; }
    public Guid QuestionSnapshotId { get; set; }
    public QuestionSnapshot? QuestionSnapshot { get; set; }

    public string SelectedJson { get; set; } = "[]";  // ["a"]
    public bool IsCorrect { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public class ScoreReport
{
    public Guid Id { get; set; }
    public Guid ExamSessionId { get; set; }
    public ExamSession? ExamSession { get; set; }

    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public double ScorePercent { get; set; }
    public bool Passed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SectionScore> SectionScores { get; set; } = new();
}

public class SectionScore
{
    public Guid Id { get; set; }
    public Guid ScoreReportId { get; set; }
    public ScoreReport? ScoreReport { get; set; }

    public string SectionSlug { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Correct { get; set; }
    public double Percent { get; set; }
}

public class Credential
{
    public Guid Id { get; set; }
    public string UserRef { get; set; } = string.Empty;
    public Guid CertificationId { get; set; }
    public Certification? Certification { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddYears(1);
    public string VerificationToken { get; set; } = Guid.NewGuid().ToString("N");
    public bool Revoked { get; set; }
}
