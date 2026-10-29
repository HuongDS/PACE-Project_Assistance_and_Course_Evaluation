using System.ComponentModel.DataAnnotations;

namespace PACE.Application.Dtos;

public record RubricCriterionDto(Guid Id, string Code, string Name, string Statement, int DisplayOrder);

public class UpsertRubricCriterionRequest
{
    [Required, MaxLength(50), RegularExpression("^[A-Z][A-Z0-9_]*$", ErrorMessage = "Code chỉ gồm chữ in hoa, số và dấu _.")]
    public string Code { get; set; } = null!;

    [Required, MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    public string Statement { get; set; } = null!;

    [Range(1, 1000)]
    public int DisplayOrder { get; set; }
}

public record BulkImportRubricCriteriaResult(
    int TotalProcessed,
    int CreatedCount,
    int UpdatedCount,
    IReadOnlyList<string> Errors
);

public record CriterionScoreDto(Guid CriterionId, int Score);

public class StudentAnswerSurveyInputDto
{
    public Guid StudentAnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public string? StudentReply { get; set; }
    public List<CriterionScoreDto> Scores { get; set; } = [];
}

public class SubmitSurveyRequest
{
    public Guid SubmissionId { get; set; }
    public List<StudentAnswerSurveyInputDto> Answers { get; set; } = [];
}

public record CriterionStatDto(
    Guid CriterionId,
    string Code,
    string Name,
    string Statement,
    double AverageScore,
    int TotalRatings,
    int Score1Count,
    int Score2Count,
    int Score3Count,
    int Score4Count,
    int Score5Count
);

public record SurveySubmissionItemDto(
    Guid SubmissionId,
    Guid StudentAnswerId,
    string StudentEmail,
    string? StudentCode,
    string? RepoName,
    string BusinessName,
    string QuestionText,
    string AnswerText,
    string? AiFeedback,
    string? StudentReply,
    double AverageScore,
    DateTime? SubmittedAt,
    IReadOnlyList<SubmissionRubricScoreDto> Scores
)
{
    public Guid AnalysisRunId { get; init; }
    public Guid BusinessId { get; init; }
    public Guid QuestionId { get; init; }
    public string? FeedbackFormat { get; init; }
    public PACE.Domain.Enums.ContextType? QuestionContextType { get; init; }
}

public record SurveySummaryResponseDto(
    int TotalSubmissions,
    int TotalEvaluatedAnswers,
    int TotalRatings,
    double OverallAverageScore,
    string HighestRatedCriterion,
    string LowestRatedCriterion,
    IReadOnlyList<CriterionStatDto> CriteriaStats,
    IReadOnlyList<SurveySubmissionItemDto> Submissions
);


public class SurveyResultQuery
{
    public Guid? AnalysisRunId { get; set; }
    public Guid? BusinessId { get; set; }
    public Guid? QuestionId { get; set; }
    public Guid? CriterionId { get; set; }
    [Range(1, 5)] public int? MinScore { get; set; }
    [Range(1, 5)] public int? MaxScore { get; set; }
    [Range(1, 5)] public double? MinAverageScore { get; set; }
    [Range(1, 5)] public double? MaxAverageScore { get; set; }
    public string? FeedbackFormat { get; set; }
    public bool? HasSurvey { get; set; }
    public bool? HasFeedback { get; set; }
    public bool ExcludeTestData { get; set; } = true;
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    [MaxLength(500)] public string? Search { get; set; }
    [MaxLength(500)] public string? StudentCode { get; set; }
    [MaxLength(500)] public string? StudentEmail { get; set; }
    [MaxLength(500)] public string? RepoName { get; set; }
    [MaxLength(500)] public string? BusinessName { get; set; }
    [MaxLength(500)] public string? QuestionText { get; set; }
    [MaxLength(500)] public string? AnswerText { get; set; }
    [MaxLength(500)] public string? AiFeedback { get; set; }
    [MaxLength(500)] public string? StudentReply { get; set; }
    [MaxLength(500)] public string? AiCriterionName { get; set; }
    public double? MinAiScore { get; set; }
    public double? MaxAiScore { get; set; }
}
public record SurveyRepoOption(Guid Id, string Name);
public record SurveyBusinessOption(Guid Id, Guid AnalysisRunId, string Name);
public record SurveyQuestionOption(Guid Id, Guid AnalysisRunId, Guid BusinessId, string Text);
public record SurveyFilterOptions(IReadOnlyList<SurveyRepoOption> Repositories,
    IReadOnlyList<SurveyBusinessOption> Businesses, IReadOnlyList<SurveyQuestionOption> Questions);
