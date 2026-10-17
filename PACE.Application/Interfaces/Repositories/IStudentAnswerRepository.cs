using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface IStudentAnswerRepository : IGenericRepository<StudentAnswer>
{
    Task<StudentAnswer?> GetByStudentAndQuestionAsync(Guid userId, Guid questionId, CancellationToken ct = default);
    Task<StudentAnswer?> GetBySubmissionAndQuestionAsync(Guid submissionId, Guid questionId, CancellationToken ct = default);
}
