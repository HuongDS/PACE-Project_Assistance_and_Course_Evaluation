using System.ComponentModel.DataAnnotations;

namespace PACE.Application.Dtos;

public class StudentIdentityRequest
{
    [Required, MaxLength(20)]
    public string StudentCode { get; set; } = "";
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = "";
}

public class SubmitStudentSubmissionRequest : StudentIdentityRequest
{
    [Required, MinLength(3), MaxLength(3)]
    public List<StudentAnswerRequest> Answers { get; set; } = [];
}

public class RetrieveStudentSubmissionRequest : StudentIdentityRequest
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

public record SubmissionRepositoryDto(Guid AnalysisRunId, string? RepoName, string? RepoUrl);
public record SubmissionQuestionDto(Guid QuestionId, string QuestionText);
public record SubmissionBusinessDto(Guid BusinessId, string BusinessName, IReadOnlyList<SubmissionQuestionDto> Questions);
public record StudentSubmissionLookupDto(SubmissionRepositoryDto Repository, IReadOnlyList<SubmissionBusinessDto> Businesses);
public record StudentSubmissionCreatedDto(Guid SubmissionId, DateTime SubmittedAt, int AnswerCount);
public record SubmittedAnswerDto(Guid BusinessId, string BusinessName, Guid QuestionId, string QuestionText, string? AnswerText);
public record StudentSubmissionDetailDto(Guid SubmissionId, DateTime SubmittedAt, SubmissionRepositoryDto Repository, IReadOnlyList<SubmittedAnswerDto> Answers);
