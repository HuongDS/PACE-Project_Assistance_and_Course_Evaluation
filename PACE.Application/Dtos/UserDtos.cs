using System.ComponentModel.DataAnnotations;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos;

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
    IReadOnlyList<ImportRowError> Skipped,
    // Existing students whose missing MSSV was filled in from the file.
    int UpdatedUsers = 0);
