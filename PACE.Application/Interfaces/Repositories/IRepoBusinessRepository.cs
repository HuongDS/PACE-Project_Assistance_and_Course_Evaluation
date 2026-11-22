using PACE.Application.Common.ReadModels;
using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface IRepoBusinessRepository
{
    Task<RepoAccessInfo?> GetAccessInfoAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<string?> GetStudentCodeAsync(Guid userId, CancellationToken ct = default);
    Task<bool> IsGroupMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default);
    Task<bool> IsStudentInGroupAsync(Guid groupId, string studentCode, CancellationToken ct = default);
    Task<bool> IsGroupTopicApprovedAsync(Guid groupId, CancellationToken ct = default);
    Task<List<RepoApiMethod>> GetApiMethodsAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<List<Business>> GetBusinessesWithApisAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<List<BusinessDiagram>> GetDiagramSummariesAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<Dictionary<Guid, BusinessCounts>> GetBusinessCountsAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<Business?> GetBusinessForUpdateAsync(Guid analysisRunId, Guid businessId, CancellationToken ct = default);
    Task<bool> BusinessExistsAsync(Guid analysisRunId, Guid businessId, CancellationToken ct = default);
    Task<bool> BusinessHasQuestionsAsync(Guid businessId, CancellationToken ct = default);
    Task<int> GetNextOrderIndexAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<BusinessDiagram?> GetDiagramAsync(Guid businessId, Guid diagramId, bool tracking, CancellationToken ct = default);
    Task<bool> DiagramTitleExistsAsync(Guid businessId, string title, Guid? exceptId, CancellationToken ct = default);
    void AddBusiness(Business business);
    void RemoveBusiness(Business business);
    void AddApi(BusinessApi api);
    void RemoveApi(BusinessApi api);
    void AddDiagram(BusinessDiagram diagram);
    void RemoveDiagram(BusinessDiagram diagram);
}
