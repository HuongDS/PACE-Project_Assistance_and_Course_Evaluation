using PACE.Domain.Entities;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Interfaces.Repositories;

public interface IStudentSubmissionRepository
{
    Task LockQuickCreateKeyAsync(string key, CancellationToken ct = default);
    Task<List<StudentAnswer>> GetSurveyAnswersAsync(CancellationToken ct = default);
    // Must be called inside a transaction; serializes submissions for this user.
    Task LockUserAsync(Guid userId, CancellationToken ct = default);
    Task<List<Business>> GetBusinessesWithQuestionsAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default);
    Task<StudentSubmission?> GetActiveSessionAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default);
    Task<StudentSubmission?> GetSubmittedAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default);
    Task<StudentSubmission?> GetForSurveyUpdateAsync(Guid id, CancellationToken ct = default);
    void AddSurveyScore(RubricScore score);
    Task<StudentSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StudentSubmission?> GetOwnedAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<PagedResult<StudentSubmission>> GetPagedAsync(StudentSubmissionQuery query, CancellationToken ct = default);
    Task<List<StudentSubmission>> GetAllSubmittedForStatsAsync(CancellationToken ct = default);
    void Add(StudentSubmission submission);
    Task UpdateAsync(StudentSubmission submission, CancellationToken ct = default);
}
