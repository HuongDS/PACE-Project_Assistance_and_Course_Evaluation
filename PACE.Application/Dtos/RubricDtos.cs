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
);

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

