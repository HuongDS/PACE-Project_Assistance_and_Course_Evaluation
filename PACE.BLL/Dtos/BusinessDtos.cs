using System.ComponentModel.DataAnnotations;
using PACE.DAL.Models;

namespace PACE.BLL.Dtos;

public record BusinessDto(
    Guid Id,
    Guid AnalysisRunId,
    string BusinessName,
    int MethodCount,
    int FlowCount);

public record BusinessDetailDto(BusinessDto Business, IReadOnlyList<BusinessFlowSummaryDto> Flows);

public class UpdateBusinessRequest
{
    [Required, MaxLength(500)]
    public string BusinessName { get; set; } = null!;
}

public record BusinessFlowSummaryDto(
    Guid Id,
    Guid BusinessId,
    string Name,
    Guid EntryMethodId,
    string EntryMethod,
    int EdgeCount,
    DateTime CreatedAt);

public record DataFlowEdgeDto(
    Guid Id,
    int StepOrder,
    Guid FromMethodId,
    string FromMethod,
    Guid ToMethodId,
    string ToMethod,
    FlowEdgeType EdgeType,
    string? ConditionContext,
    string? DataLabel);

public record BusinessFlowDetailDto(
    BusinessFlowSummaryDto Flow,
    string? MermaidGraph,
    IReadOnlyList<DataFlowEdgeDto> Edges);
