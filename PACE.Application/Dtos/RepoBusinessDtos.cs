using System.ComponentModel.DataAnnotations;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos;

public record RepoApiDto(
    Guid MethodSourceId,
    string Controller,
    string Function,
    string? HttpMethod,
    string? Path,
    string? FilePath,
    IReadOnlyList<Guid> BusinessIds);

public record BusinessApiDto(
    Guid Id,
    string Controller,
    string Function,
    string? HttpMethod,
    string? Path,
    int OrderIndex,
    bool FoundInCode,
    Guid? MethodSourceId);

public record BusinessDiagramSummaryDto(
    Guid Id,
    Guid BusinessId,
    string Title,
    DiagramFormat Format,
    string? PreviewUrl,
    string? Description,
    int NodeCount,
    int EdgeCount,
    DateTime UpdatedAt);

public record BusinessDiagramDto(
    BusinessDiagramSummaryDto Diagram,
    string? SourceXml,
    string? NormalizedFlowText);

public record RepoBusinessDto(
    Guid Id,
    Guid AnalysisRunId,
    string BusinessName,
    string? Description,
    int OrderIndex,
    string? OwnerStudentCode,
    int MethodCount,
    int FlowCount,
    int QuestionCount,
    IReadOnlyList<BusinessApiDto> Apis,
    IReadOnlyList<BusinessDiagramSummaryDto> Diagrams,
    DateTime UpdatedAt);

public record RepoBusinessWorkspaceDto(
    Guid AnalysisRunId,
    Guid? GroupId,
    bool CanEdit,
    DateTime? AnalyzedAt,
    string? AnalyzedCommitSha,
    IReadOnlyList<RepoBusinessDto> Businesses);

public class SaveRepoBusinessRequest
{
    [Required, MaxLength(500)]
    public string BusinessName { get; set; } = null!;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string? OwnerStudentCode { get; set; }

    [Range(0, 10000)]
    public int? OrderIndex { get; set; }

    [MaxLength(200)]
    public List<Guid> MethodSourceIds { get; set; } = [];

    [MaxLength(200)]
    public List<Guid> KeepApiIds { get; set; } = [];
}

public class SaveBusinessDiagramRequest
{
    [Required, MaxLength(255)]
    public string Title { get; set; } = null!;

    public DiagramFormat Format { get; set; } = DiagramFormat.Drawio;

    public string? SourceXml { get; set; }

    [MaxLength(1000)]
    public string? PreviewUrl { get; set; }

    [MaxLength(4000)]
    public string? Description { get; set; }
}
