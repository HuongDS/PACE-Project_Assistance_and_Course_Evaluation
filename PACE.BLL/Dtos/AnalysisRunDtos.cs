using System.ComponentModel.DataAnnotations;
using PACE.BLL.Common;
using PACE.DAL.Models;

namespace PACE.BLL.Dtos;

public record AnalysisRunDto(
    Guid Id,
    string? RepoUrl,
    string? RepoName,
    bool IsAnalyzing,
    DateTime CreatedAt,
    int StudentCount,
    int BusinessCount,
    int MethodCount);

public record AnalysisRunDetailDto(
    AnalysisRunDto Run,
    IReadOnlyList<BusinessDto> Businesses,
    IReadOnlyList<string> StudentEmails);

public class CreateAnalysisRunRequest
{
    [Required, Url, MaxLength(500)]
    public string RepoUrl { get; set; } = null!;

    [MaxLength(255)]
    public string? RepoName { get; set; }
}

public class UpdateAnalysisRunRequest
{
    [Required, Url, MaxLength(500)]
    public string RepoUrl { get; set; } = null!;

    [MaxLength(255)]
    public string? RepoName { get; set; }
}

public class AnalysisRunQuery : PageQuery
{
}

public record AnalyzeResultDto(
    Guid AnalysisRunId,
    string DetectedStacks,
    int MethodCount,
    int BusinessCount,
    int FlowCount,
    int EdgeCount,
    IReadOnlyList<string> Warnings);
