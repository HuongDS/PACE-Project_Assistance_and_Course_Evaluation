using System.ComponentModel.DataAnnotations;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos;

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

    /// <summary>Template đã chốt (từ POST /api/templates/generate); bỏ trống thì đọc template_business.json trong repo.</summary>
    public List<BusinessTemplate>? Businesses { get; set; }
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
