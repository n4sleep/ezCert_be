namespace EzCert.Processor.Features.Attempts;

// One take of an exam by one device (AD-5). Snapshot-based; scored server-side.
public class Attempt
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Exams.Exam? Exam { get; set; }
    public string DeviceId { get; set; } = "";
    public string Status { get; set; } = "in_progress"; // in_progress | submitted | expired
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public double ScorePercent { get; set; }
    public bool Passed { get; set; }

    public List<AttemptQuestion> Questions { get; set; } = new();
    public List<SectionScore> SectionScores { get; set; } = new();
}

// Immutable snapshot of the question at attempt start (AD-5).
public class AttemptQuestion
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Attempt? Attempt { get; set; }
    public Guid? SourceQuestionId { get; set; }
    public int Ordinal { get; set; }
    public string Section { get; set; } = "";
    public string QuestionJson { get; set; } = "";    // { type, text }
    public string ChoicesJson { get; set; } = "";     // [{label,text}]
    public string CorrectJson { get; set; } = "";     // ["a","b"]
    public string Explanation { get; set; } = "";
    public string CitationJson { get; set; } = "[]";
    public Answer? Answer { get; set; }
}

public class Answer
{
    public Guid Id { get; set; }
    public Guid AttemptQuestionId { get; set; }
    public AttemptQuestion? AttemptQuestion { get; set; }
    public string SelectedJson { get; set; } = "[]";
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
}

public class SectionScore
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Attempt? Attempt { get; set; }
    public string Section { get; set; } = "";
    public int Total { get; set; }
    public int Correct { get; set; }
    public double Percentage { get; set; }
}
