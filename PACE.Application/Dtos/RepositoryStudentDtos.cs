using System.ComponentModel.DataAnnotations;

namespace PACE.Application.Dtos;

public record RepositoryStudentDto(Guid UserId, string? StudentCode, string Email, Guid? AnalysisRunId, string? RepoName);
public class AssignRepositoryStudentsRequest
{
    [Required, MinLength(1), MaxLength(100)]
    public List<string> StudentCodes { get; set; } = [];
}
public record AssignRepositoryStudentsResult(int Total, int Created, int Moved, int AlreadyAssigned);
