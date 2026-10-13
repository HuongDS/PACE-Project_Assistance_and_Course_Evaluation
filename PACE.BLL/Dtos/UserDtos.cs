using System.ComponentModel.DataAnnotations;
using PACE.BLL.Common;
using PACE.DAL.Models;

namespace PACE.BLL.Dtos;

public record UserDto(
    Guid Id,
    string Email,
    UserRole Role,
    Guid? AnalysisRunId,
    string? RepoName,
    string? RepoUrl,
    DateTime CreatedAt);

public class CreateUserRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = null!;

    public UserRole Role { get; set; } = UserRole.Student;

    public Guid? AnalysisRunId { get; set; }
}

public class UpdateUserRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = null!;

    public UserRole Role { get; set; } = UserRole.Student;

    public Guid? AnalysisRunId { get; set; }
}

public class UserQuery : PageQuery
{
    public UserRole? Role { get; set; }
    public Guid? AnalysisRunId { get; set; }
}

public record ImportRowError(int Row, string? Email, string? RepoUrl, string Reason);

public record ImportUsersResult(
    int TotalRows,
    int CreatedUsers,
    int CreatedRepos,
    IReadOnlyList<ImportRowError> Skipped);
