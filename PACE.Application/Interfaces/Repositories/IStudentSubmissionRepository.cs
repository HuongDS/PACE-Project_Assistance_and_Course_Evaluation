using PACE.Domain.Entities;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Interfaces.Repositories;

public interface IStudentSubmissionRepository
{
    // Must be called inside a transaction; serializes submissions for this user.
    Task LockUserAsync(Guid userId, CancellationToken ct = default);
    Task<List<Business>> GetBusinessesWithQuestionsAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default);
    Task<StudentSubmission?> GetActiveSessionAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default);
    Task<StudentSubmission?> GetSubmittedAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default);
    Task<StudentSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StudentSubmission?> GetOwnedAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<PagedResult<StudentSubmission>> GetPagedAsync(StudentSubmissionQuery query, CancellationToken ct = default);
    void Add(StudentSubmission submission);
    void Update(StudentSubmission submission);
}
