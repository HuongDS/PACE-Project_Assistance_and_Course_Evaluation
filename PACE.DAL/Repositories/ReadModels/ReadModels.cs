using PACE.DAL.Models;

namespace PACE.DAL.Repositories.ReadModels;

public record AnalysisRunSummary(
    Guid Id,
    string? RepoUrl,
    string? RepoName,
    DateTime CreatedAt,
    int StudentCount,
    int BusinessCount,
    int MethodCount);

public record BusinessSummary(
    Guid Id,
    Guid AnalysisRunId,
    string BusinessName,
    int MethodCount,
    int FlowCount);

public record BusinessFlowSummary(
    Guid Id,
    Guid BusinessId,
    string Name,
    Guid EntryMethodId,
    string EntryMethod,
    int EdgeCount,
    DateTime CreatedAt);

public record DataFlowEdgeDetail(
    Guid Id,
    int StepOrder,
    Guid FromMethodId,
    string FromMethod,
    Guid ToMethodId,
    string ToMethod,
    FlowEdgeType EdgeType,
    string? ConditionContext,
    string? DataLabel);

public record MethodSourceSummary(
    Guid Id,
    Guid AnalysisRunId,
    string ClassName,
    string MethodName,
    string? FilePath,
    ProgrammingLanguage Language,
    NodeType NodeType,
    string? Parameters);
