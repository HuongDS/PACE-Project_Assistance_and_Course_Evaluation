using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IRepoBusinessService
{
    Task<RepoBusinessWorkspaceDto> GetWorkspaceAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<IReadOnlyList<RepoApiDto>> GetApisAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<AnalyzeResultDto> ReloadSourceAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<RepoBusinessDto> GetBusinessAsync(Guid analysisRunId, Guid businessId, CancellationToken ct = default);
    Task<RepoBusinessDto> CreateBusinessAsync(Guid analysisRunId, SaveRepoBusinessRequest request, CancellationToken ct = default);
    Task<RepoBusinessDto> UpdateBusinessAsync(Guid analysisRunId, Guid businessId, SaveRepoBusinessRequest request, CancellationToken ct = default);
    Task DeleteBusinessAsync(Guid analysisRunId, Guid businessId, CancellationToken ct = default);
    Task<BusinessDiagramDto> GetDiagramAsync(Guid analysisRunId, Guid businessId, Guid diagramId, CancellationToken ct = default);
    Task<BusinessDiagramDto> CreateDiagramAsync(Guid analysisRunId, Guid businessId, SaveBusinessDiagramRequest request, CancellationToken ct = default);
    Task<BusinessDiagramDto> UpdateDiagramAsync(Guid analysisRunId, Guid businessId, Guid diagramId, SaveBusinessDiagramRequest request, CancellationToken ct = default);
    Task DeleteDiagramAsync(Guid analysisRunId, Guid businessId, Guid diagramId, CancellationToken ct = default);
}
