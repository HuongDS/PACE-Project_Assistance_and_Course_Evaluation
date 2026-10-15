using PACE.Application.Common.ReadModels;
using PACE.Application.Dtos;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Mappings;

public static class DtoMappings
{
    public static UserDto ToDto(this User user) => new(
        user.Id,
        user.Email,
        user.Role,
        user.AnalysisRunId,
        user.AnalysisRun?.RepoName,
        user.AnalysisRun?.RepoUrl,
        user.CreatedAt);

    public static RubricCriterionDto ToDto(this RubricCriterion c) => new(c.Id, c.Code, c.Name, c.Statement, c.DisplayOrder);

    public static AnalysisRunDto ToDto(this AnalysisRunSummary r, bool isAnalyzing) => new(
        r.Id, r.RepoUrl, r.RepoName, isAnalyzing, r.CreatedAt, r.StudentCount, r.BusinessCount, r.MethodCount);

    public static BusinessDto ToDto(this BusinessSummary b) => new(b.Id, b.AnalysisRunId, b.BusinessName, b.MethodCount, b.FlowCount);

    public static BusinessFlowSummaryDto ToDto(this BusinessFlowSummary f) => new(
        f.Id, f.BusinessId, f.Name, f.EntryMethodId, f.EntryMethod, f.EdgeCount, f.CreatedAt);

    public static DataFlowEdgeDto ToDto(this DataFlowEdgeDetail e) => new(
        e.Id, e.StepOrder, e.FromMethodId, e.FromMethod, e.ToMethodId, e.ToMethod, e.EdgeType, e.ConditionContext, e.DataLabel);

    public static MethodSourceSummaryDto ToDto(this MethodSourceSummary m) => new(
        m.Id, m.AnalysisRunId, m.ClassName, m.MethodName, m.FilePath, m.Language, m.NodeType, m.Parameters);

    public static MethodSourceSummaryDto ToSummaryDto(this MethodSource m) => new(
        m.Id, m.AnalysisRunId, m.ClassName, m.MethodName, m.FilePath, m.Language, m.NodeType, m.Parameters);

    public static GeneratedQuestionDto ToDto(this GeneratedQuestionSummary q) => new(
        q.Id, q.BusinessId, q.QuestionText, q.SuggestedAnswer, q.Difficulty, q.ContextType, q.CreatedAt);

    public static GeneratedQuestionDto ToDto(this GeneratedQuestion q, IReadOnlyList<string>? targetedEntryPoints = null) => new(
        q.Id, q.BusinessId, q.QuestionText, q.SuggestedAnswer, q.Difficulty, q.ContextType, q.CreatedAt, targetedEntryPoints);
}
