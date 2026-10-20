using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PACE.Application.Dtos.StudentAnswer;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos;

public class SubmitStudentSubmissionRequest
{
    public Guid? SubmissionId { get; set; }

    [Required, MinLength(1), MaxLength(4)]
    public List<StudentAnswerRequest> Answers { get; set; } = [];
}

public class RetrieveStudentSubmissionRequest
{
    public Guid SubmissionId { get; set; }
}

public class StudentAnswerRequest
{
    public Guid BusinessId { get; set; }
    public Guid QuestionId { get; set; }
    [Required, MaxLength(20000)]
    public string AnswerText { get; set; } = "";
}

public class JoinStudentSubmissionRequest
{
    [Required, MaxLength(20)]
    public string StudentCode { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class QuickCreateStudentSubmissionRequest
{
    [Required, MaxLength(20)]
    public string StudentCode { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, Url, MaxLength(500)]
    public string RepoUrl { get; set; } = string.Empty;
}

public record StudentSubmissionJoinedDto(
    Guid SubmissionId,
    DateTime CreatedAt,
    string StudentCode,
    string Email,
    string? RepoName
)
{
    public DateTime? SubmittedAt { get; init; }
}

public record SubmissionRepositoryDto(Guid AnalysisRunId, string? RepoName, string? RepoUrl);
public record SubmissionQuestionDto(Guid QuestionId, string QuestionText);
public record SubmissionBusinessDto(Guid BusinessId, string BusinessName, IReadOnlyList<SubmissionQuestionDto> Questions);
public record StudentSubmissionLookupDto(SubmissionRepositoryDto Repository, IReadOnlyList<SubmissionBusinessDto> Businesses);
public record StudentSubmissionCreatedDto(Guid SubmissionId, DateTime? SubmittedAt, int AnswerCount);
public record SubmittedAnswerDto(Guid BusinessId, string BusinessName, Guid QuestionId, string QuestionText, string? AnswerText, string? AiFeedback = null)
{
    public Guid StudentAnswerId { get; init; }
    public string? SuggestedAnswer { get; init; }
    public string? Difficulty { get; init; }
    public ContextType ContextType { get; init; }
    public DateTime QuestionCreatedAt { get; init; }
    public DateTime AnswerCreatedAt { get; init; }
    public string? FeedbackFormat { get; init; }
    public string? StudentReply { get; init; }
    public IReadOnlyList<SubmissionRubricScoreDto> RubricScores { get; init; } = [];
}

public record SubmissionRubricScoreDto(Guid Id, Guid CriterionId, string Code, string Name, string Statement, int Score);
public record StudentSubmissionDetailDto(Guid SubmissionId, DateTime CreatedAt, DateTime? SubmittedAt, SubmissionRepositoryDto Repository, IReadOnlyList<SubmittedAnswerDto> Answers)
{
    public SubmissionStudentDto Student { get; init; } = null!;
    public bool SurveyCompleted => Answers.Any(a => a.RubricScores.Count > 0);
}
public record SubmissionStudentDto(Guid UserId, string? StudentCode, string Email);

public class EvaluatedAnswerDto
{
    public Guid StudentAnswerId { get; set; }
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string AnswerText { get; set; } = string.Empty;
    public ContextType ContextType { get; set; } = ContextType.Raw;
    public string? StudentReply { get; set; }
    public IReadOnlyList<SubmissionRubricScoreDto> RubricScores { get; set; } = [];
    public StudentAnswerRes? AiEvaluation { get; set; }
    public string EvaluationStatus => AiEvaluation?.HasContent == true ? "Completed" : "Unavailable";
    public string? EvaluationError { get; set; }
}

public class StudentSubmissionResultDto
{
    public Guid SubmissionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public SubmissionRepositoryDto Repository { get; set; } = null!;
    public List<EvaluatedAnswerDto> EvaluatedAnswers { get; set; } = new();
    public bool SurveyCompleted => EvaluatedAnswers.Any(a => a.RubricScores.Count > 0);
}

public class StudentSubmissionQuery : PACE.Application.Common.Models.PageQuery
{
    public Guid? UserId { get; set; }
    public Guid? AnalysisRunId { get; set; }
    public bool? IsSubmitted { get; set; }
    public DateOnly? Date { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public record AdminStudentSubmissionDto(Guid UserId, string? StudentId, string Email, StudentSubmissionDetailDto Submission);

