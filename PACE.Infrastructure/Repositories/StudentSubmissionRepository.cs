using Microsoft.EntityFrameworkCore;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class StudentSubmissionRepository(AppDbContext context) : IStudentSubmissionRepository
{
    public async Task LockUserAsync(Guid userId, CancellationToken ct = default)
    {
        // Materialize directly: FOR UPDATE must execute on the same context/transaction as SaveChanges.
        await context.Users.FromSqlInterpolated($"SELECT * FROM users WHERE \"Id\" = {userId} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
    }

    public Task<List<Business>> GetBusinessesWithQuestionsAsync(Guid analysisRunId, CancellationToken ct = default)
        => context.Businesses.AsNoTracking().Where(b => b.AnalysisRunId == analysisRunId && b.Questions.Any())
            .Include(b => b.Questions).OrderBy(b => b.BusinessName).ThenBy(b => b.Id).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default)
        => context.StudentSubmissions.AnyAsync(s => s.UserId == userId && s.AnalysisRunId == analysisRunId, ct);

    public Task<StudentSubmission?> GetOwnedAsync(Guid id, Guid userId, CancellationToken ct = default)
        => context.StudentSubmissions.AsNoTracking().Include(s => s.AnalysisRun)
            .Include(s => s.Answers).ThenInclude(a => a.Question).ThenInclude(q => q.Business)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

    public void Add(StudentSubmission submission) => context.StudentSubmissions.Add(submission);
}
