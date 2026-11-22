namespace PACE.Application.Common.ReadModels;

public record RepoAccessInfo(
    Guid AnalysisRunId,
    Guid? GroupId,
    bool IsArchived,
    string? RepoUrl,
    DateTime? AnalyzedAt,
    string? AnalyzedCommitSha);

public record RepoApiMethod(
    Guid Id,
    string ClassName,
    string MethodName,
    string? HttpVerb,
    string? Route,
    string? FilePath);

public record BusinessCounts(
    Guid BusinessId,
    int MethodCount,
    int FlowCount,
    int QuestionCount);
