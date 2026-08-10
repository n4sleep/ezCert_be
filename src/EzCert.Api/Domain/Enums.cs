namespace EzCert.Api.Domain;

public enum QuestionType
{
    Single,
    Multi,
    TrueFalse
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

public enum ExamMode
{
    Practice,
    Certification
}

public enum SessionStatus
{
    InProgress,
    Submitted,
    Expired
}
