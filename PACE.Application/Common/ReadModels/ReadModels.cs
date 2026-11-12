using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Common.ReadModels;

public record AnalysisRunSummary(
    Guid Id,
    string? RepoUrl,
    string? RepoName,
    DateTime CreatedAt,
    int StudentCount,
    int BusinessCount,
    int MethodCount,
    string? GroupName = null,
    string? TopicName = null,
    Guid? ClassId = null,
    string? ClassName = null);

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

public record FlowQuestionContext(
    Guid Id,
    string Name,
    Guid EntryMethodId,
    string? MermaidGraph,
    IReadOnlyList<FlowEdgeRef> Edges);

public record FlowEdgeRef(int StepOrder, Guid FromMethodId, Guid ToMethodId);

public record MethodCode(Guid Id, string ClassName, string MethodName, string SourceCode);

public record GeneratedQuestionSummary(
    Guid Id,
    Guid BusinessId,
    string QuestionText,
    string? SuggestedAnswer,
    string? Difficulty,
    ContextType ContextType,
    DateTime CreatedAt,
    int AnswerCount);
