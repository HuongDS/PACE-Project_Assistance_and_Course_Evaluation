using Microsoft.EntityFrameworkCore;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
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
        => context.StudentSubmissions.AnyAsync(s => s.UserId == userId && s.AnalysisRunId == analysisRunId && s.SubmittedAt != null, ct);

    public Task<StudentSubmission?> GetActiveSessionAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default)
        => context.StudentSubmissions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AnalysisRunId == analysisRunId && s.SubmittedAt == null, ct);

    public Task<StudentSubmission?> GetSubmittedAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default)
        => context.StudentSubmissions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AnalysisRunId == analysisRunId && s.SubmittedAt != null, ct);

    public Task<StudentSubmission?> GetOwnedAsync(Guid id, Guid userId, CancellationToken ct = default)
        => context.StudentSubmissions.AsNoTracking().Include(s => s.AnalysisRun)
            .Include(s => s.Answers).ThenInclude(a => a.Question).ThenInclude(q => q.Business)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

    public async Task<PagedResult<StudentSubmission>> GetPagedAsync(StudentSubmissionQuery query, CancellationToken ct = default)
    {
        var source = context.StudentSubmissions.AsNoTracking().AsQueryable();
        if (query.UserId.HasValue) source = source.Where(s => s.UserId == query.UserId.Value);
        if (query.AnalysisRunId.HasValue) source = source.Where(s => s.AnalysisRunId == query.AnalysisRunId.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(s => s.User.Email.ToLower().Contains(search)
                || (s.User.StudentCode != null && s.User.StudentCode.ToLower().Contains(search)));
        }
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(s => s.SubmittedAt ?? s.CreatedAt).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(s => s.User).Include(s => s.AnalysisRun)
            .Include(s => s.Answers).ThenInclude(a => a.Question).ThenInclude(q => q.Business)
            .ToListAsync(ct);
        return new PagedResult<StudentSubmission> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public void Add(StudentSubmission submission) => context.StudentSubmissions.Add(submission);
    public void Update(StudentSubmission submission) => context.StudentSubmissions.Update(submission);
}
