using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Interfaces.Repositories;

public interface IAnalysisRunRepository : IGenericRepository<AnalysisRun>
{
    Task<PagedResult<AnalysisRunSummary>> SearchSummariesAsync(
        string? search,
        int page,
        int pageSize,
        Guid? classId = null,
        IReadOnlyList<Guid>? allowedClassIds = null,
        Guid? singleRepoId = null,
        IReadOnlyList<Guid>? allowedRepoIds = null,
        CancellationToken ct = default);
    Task<AnalysisRunSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default);
    Task<AnalysisRun?> GetByRepoUrlAsync(string repoUrl, CancellationToken ct = default);
    Task<bool> RepoUrlExistsAsync(string repoUrl, Guid? exceptId = null, CancellationToken ct = default);
    Task<List<AnalysisRun>> GetAllWithRepoUrlAsync(CancellationToken ct = default);
    Task<List<string>> GetAllRepoNamesAsync(CancellationToken ct = default);
    Task ArchiveCurrentAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<bool> HasQuestionsAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<bool> IsLecturerOfClassAsync(Guid classId, Guid lecturerUserId, CancellationToken ct = default);
    Task<bool> IsLecturerOfRepoClassAsync(Guid repoId, Guid lecturerUserId, CancellationToken ct = default);
    Task<List<Guid>> GetLecturerClassIdsAsync(Guid lecturerUserId, CancellationToken ct = default);
    Task<bool> IsStudentOfRepoAsync(Guid repoId, Guid studentUserId, CancellationToken ct = default);
    Task<Guid?> GetStudentRepoIdAsync(Guid studentUserId, CancellationToken ct = default);
    Task<List<Guid>> GetStudentRepoIdsAsync(Guid studentUserId, CancellationToken ct = default);
    Task<Guid?> GetStudentRepoIdInClassAsync(Guid studentUserId, Guid classId, CancellationToken ct = default);
    Task<List<Guid>> GetStudentRepoIdsInClassAsync(Guid studentUserId, Guid classId, CancellationToken ct = default);
    Task<Guid?> GetStudentGroupIdInClassAsync(Guid studentUserId, Guid classId, CancellationToken ct = default);
}
