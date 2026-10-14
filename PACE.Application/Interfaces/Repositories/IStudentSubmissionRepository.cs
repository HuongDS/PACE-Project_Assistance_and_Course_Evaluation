using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface IStudentSubmissionRepository
{
    // Must be called inside a transaction; serializes submissions for this user.
    Task LockUserAsync(Guid userId, CancellationToken ct = default);
    Task<List<Business>> GetBusinessesWithQuestionsAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default);
    Task<StudentSubmission?> GetOwnedAsync(Guid id, Guid userId, CancellationToken ct = default);
    void Add(StudentSubmission submission);
}
