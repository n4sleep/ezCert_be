namespace EzCert.Api.Contracts;

// ---- Catalog DTOs ----
public record ChoiceDto(string Label, string Text, bool IsCorrect, int Ordinal);

public record QuestionDto(
    Guid Id,
    string ExternalId,
    string Type,
    string Difficulty,
    string Text,
    string Explanation,
    string? Source,
    IReadOnlyList<ChoiceDto> Choices);

public record SectionDto(Guid Id, string Slug, string Name, int Ordinal);

public record ExamDto(Guid Id, string Name, int PassPercent, int? TimeLimitMinutes, IReadOnlyList<SectionDto> Sections);

public record CertificationDto(Guid Id, string Code, string Name, string? Description, IReadOnlyList<ExamDto> Exams);

public record CreateCertificationRequest(string Code, string Name, string? Description);
public record UpdateCertificationRequest(string Name, string? Description);
public record CreateExamRequest(string Name, int PassPercent, int? TimeLimitMinutes);
public record CreateSectionRequest(string Slug, string Name, int Ordinal);

// ---- Session DTOs ----
public record StartSessionRequest(Guid ExamId, string Mode, IReadOnlyList<string>? SectionSlugs, int? QuestionCount, string? UserRef);

public record SessionChoiceDto(string Label, string Text);

public record SessionQuestionDto(
    Guid SnapshotId,
    int Ordinal,
    string SectionSlug,
    string SectionName,
    string Type,
    string Text,
    IReadOnlyList<SessionChoiceDto> Choices);

public record SessionDto(
    Guid Id,
    Guid ExamId,
    string Mode,
    string Status,
    DateTime StartedAt,
    DateTime? ExpiresAt,
    IReadOnlyList<SessionQuestionDto> Questions);

public record SubmitAnswerRequest(Guid SnapshotId, IReadOnlyList<string> Selected);

public record AnswerResultDto(
    Guid SnapshotId,
    bool Answered,
    bool? IsCorrect,
    IReadOnlyList<string>? Correct,
    string? Explanation,
    string? Source);

public record SectionScoreDto(string SectionSlug, string SectionName, int Total, int Correct, double Percent);

public record ScoreReportDto(
    Guid SessionId,
    int TotalQuestions,
    int CorrectCount,
    double ScorePercent,
    bool Passed,
    int PassPercent,
    IReadOnlyList<SectionScoreDto> Sections,
    IReadOnlyList<ReviewItemDto> Review);

public record ReviewItemDto(
    int Ordinal,
    string Text,
    IReadOnlyList<string> Selected,
    IReadOnlyList<string> Correct,
    bool IsCorrect,
    string Explanation,
    string? Source);
