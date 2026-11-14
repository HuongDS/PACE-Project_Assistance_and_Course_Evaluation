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
    public Guid? SemesterId { get; set; }
    public string? Semester { get; set; }
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

public class CreateClassRequest
{
    [Required(ErrorMessage = "Tên lớp học không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên lớp học tối đa 100 ký tự.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Mã môn học (CourseId) không được để trống.")]
    public Guid CourseId { get; set; }

    public Guid? SemesterId { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class ClassImportRow
{
    public int Row { get; set; }
    public string ClassName { get; set; } = null!;
    public string CourseCode { get; set; } = null!;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public ClassImportRow(int row, string className, string courseCode, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        Row = row;
        ClassName = className;
        CourseCode = courseCode;
        StartDate = startDate;
        EndDate = endDate;
    }
}

public class ImportClassesResult
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<ClassDto> CreatedClasses { get; set; } = [];

    public ImportClassesResult(int totalRows, int successCount, int skippedCount, List<string>? errors = null, List<ClassDto>? createdClasses = null)
    {
        TotalRows = totalRows;
        SuccessCount = successCount;
        SkippedCount = skippedCount;
        Errors = errors ?? [];
        CreatedClasses = createdClasses ?? [];
    }
}

