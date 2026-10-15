using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.ReadModels;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class GeneratedQuestionRepository(AppDbContext context) : GenericRepository<GeneratedQuestion>(context), IGeneratedQuestionRepository
{
    public Task<List<GeneratedQuestionSummary>> GetSummariesByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => DbSet.AsNoTracking()
            .Where(q => q.BusinessId == businessId)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new GeneratedQuestionSummary(
                q.Id, q.BusinessId, q.QuestionText, q.SuggestedAnswer, q.Difficulty,
                q.ContextType, q.CreatedAt))
            .ToListAsync(ct);

    public Task<int> CountAnswersAsync(Guid questionId, CancellationToken ct = default)
        => Context.StudentAnswers.CountAsync(a => a.QuestionId == questionId, ct);
}
