using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PACE.Application.Dtos.StudentAnswer;

namespace PACE.Application.Dtos;

public class SubmitStudentSubmissionRequest
{
    public Guid? SubmissionId { get; set; }

    [Required, MinLength(3), MaxLength(3)]
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
    [Required]
    public string AnswerText { get; set; } = "";
}

public class JoinStudentSubmissionRequest
{
    [Required]
    public string StudentCode { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public record StudentSubmissionJoinedDto(
    Guid SubmissionId,
    DateTime CreatedAt,
    string StudentCode,
    string Email,
    string? RepoName
);

public record SubmissionRepositoryDto(Guid AnalysisRunId, string? RepoName, string? RepoUrl);
public record SubmissionQuestionDto(Guid QuestionId, string QuestionText);
public record SubmissionBusinessDto(Guid BusinessId, string BusinessName, IReadOnlyList<SubmissionQuestionDto> Questions);
public record StudentSubmissionLookupDto(SubmissionRepositoryDto Repository, IReadOnlyList<SubmissionBusinessDto> Businesses);
public record StudentSubmissionCreatedDto(Guid SubmissionId, DateTime? SubmittedAt, int AnswerCount);
public record SubmittedAnswerDto(Guid BusinessId, string BusinessName, Guid QuestionId, string QuestionText, string? AnswerText, string? AiFeedback = null);
public record StudentSubmissionDetailDto(Guid SubmissionId, DateTime CreatedAt, DateTime? SubmittedAt, SubmissionRepositoryDto Repository, IReadOnlyList<SubmittedAnswerDto> Answers);

public class EvaluatedAnswerDto
{
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public Guid QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string AnswerText { get; set; } = string.Empty;
    public StudentAnswerRes? AiEvaluation { get; set; }
}

public class StudentSubmissionResultDto
{
    public Guid SubmissionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public SubmissionRepositoryDto Repository { get; set; } = null!;
    public List<EvaluatedAnswerDto> EvaluatedAnswers { get; set; } = new();
}

public class StudentSubmissionQuery : PACE.Application.Common.Models.PageQuery
{
    public Guid? UserId { get; set; }
    public Guid? AnalysisRunId { get; set; }
}

public record AdminStudentSubmissionDto(Guid UserId, string? StudentId, string Email, StudentSubmissionDetailDto Submission);
