using System.ComponentModel.DataAnnotations;
using PACE.Application.Common.Models;

namespace PACE.Application.Dtos;

public record ClassDto(
    Guid Id,
    string? Name,
    Guid CourseId,
    string? CourseCode,
    string? CourseName,
    Guid? LecturerId,
    string? LecturerName,
    string? LecturerEmail,
    string? Semester,
    DateOnly StartDate,
    DateOnly EndDate,
    int StudentCount,
    int GroupCount);

public record ClassStudentDto(
    string StudentCode,
    Guid? UserId,
    string? FullName,
    string? Email,
    int Status,
    string? GroupName,
    DateTime? EnrolledAt);

public record ClassGroupDto(
    Guid Id,
    string Name,
    string? TopicName,
    Guid? AnalysisRunId,
    string? RepoName,
    string? RepoUrl,
    int MemberCount);

public record ClassDetailDto(
    ClassDto Class,
    IReadOnlyList<ClassStudentDto> Students,
    IReadOnlyList<ClassGroupDto> Groups);

public class ClassQuery : PageQuery
{
    public string? SearchTerm { get; set; }
    public Guid? CourseId { get; set; }
}

public class ClassStudentQuery : PageQuery
{
    public string? SearchTerm { get; set; }
}

public class AddStudentsToClassRequest
{
    [Required]
    public List<string> StudentCodes { get; set; } = [];
}

public class ImportClassStudentsResult
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int AlreadyInClassCount { get; set; }
    public int NewUsersCreatedCount { get; set; }
    public List<string> Errors { get; set; } = [];

    public ImportClassStudentsResult(int totalRows, int successCount, int alreadyInClassCount, int newUsersCreatedCount, List<string>? errors = null)
    {
        TotalRows = totalRows;
        SuccessCount = successCount;
        AlreadyInClassCount = alreadyInClassCount;
        NewUsersCreatedCount = newUsersCreatedCount;
        Errors = errors ?? [];
    }
}

