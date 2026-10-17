using Microsoft.EntityFrameworkCore;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class StudentAnswerRepository(AppDbContext context) : GenericRepository<StudentAnswer>(context), IStudentAnswerRepository
{
    public Task<StudentAnswer?> GetByStudentAndQuestionAsync(Guid userId, Guid questionId, CancellationToken ct = default)
        => DbSet.OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(a => a.UserId == userId && a.QuestionId == questionId, ct);

    public Task<StudentAnswer?> GetBySubmissionAndQuestionAsync(Guid submissionId, Guid questionId, CancellationToken ct = default)
        => DbSet.FirstOrDefaultAsync(a => a.SubmissionId == submissionId && a.QuestionId == questionId, ct);
}
