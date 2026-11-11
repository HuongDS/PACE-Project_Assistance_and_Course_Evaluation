using Microsoft.EntityFrameworkCore;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class StudentSubmissionRepository(AppDbContext context) : IStudentSubmissionRepository
{
    public async Task LockQuickCreateKeyAsync(string key, CancellationToken ct = default)
        => await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

    public Task<List<StudentAnswer>> GetSurveyAnswersAsync(CancellationToken ct = default)
        => context.StudentAnswers.AsNoTracking().AsSplitQuery()
            .Where(a => a.Submission != null && a.Submission.SubmittedAt != null)
            .Include(a => a.Submission).ThenInclude(s => s!.AnalysisRun)
            .Include(a => a.User).Include(a => a.Question).ThenInclude(q => q.Business)
            .OrderByDescending(a => a.Submission!.SubmittedAt).ThenBy(a => a.Id).ToListAsync(ct);

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
        => context.StudentSubmissions.AsNoTracking().Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AnalysisRunId == analysisRunId && s.SubmittedAt == null, ct);

    public Task<StudentSubmission?> GetSubmittedAsync(Guid userId, Guid analysisRunId, CancellationToken ct = default)
        => Details().OrderByDescending(s => s.SubmittedAt).ThenBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AnalysisRunId == analysisRunId && s.SubmittedAt != null, ct);

    private IQueryable<StudentSubmission> Details()
        => context.StudentSubmissions.AsNoTracking().AsSplitQuery()
            .Include(s => s.User).Include(s => s.AnalysisRun)
            .Include(s => s.Answers).ThenInclude(a => a.Question).ThenInclude(q => q.Business);

    public Task<StudentSubmission?> GetForSurveyUpdateAsync(Guid id, CancellationToken ct = default)
        => context.StudentSubmissions.AsTracking().AsSplitQuery()
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<StudentSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Details().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<StudentSubmission?> GetOwnedAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Details().FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

    public async Task<PagedResult<StudentSubmission>> GetPagedAsync(StudentSubmissionQuery query, CancellationToken ct = default)
    {
        var source = Details();
        if (query.IsSubmitted.HasValue) source = source.Where(s => (s.SubmittedAt != null) == query.IsSubmitted.Value);
        if (query.UserId.HasValue) source = source.Where(s => s.UserId == query.UserId.Value);
        if (query.AnalysisRunId.HasValue) source = source.Where(s => s.AnalysisRunId == query.AnalysisRunId.Value);

        if (query.FromDate.HasValue && query.ToDate.HasValue)
        {
            var from = DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc);
            var to = DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc);
            source = source.Where(s =>
                (s.CreatedAt >= from && s.CreatedAt <= to) ||
                (s.SubmittedAt != null && s.SubmittedAt.Value >= from && s.SubmittedAt.Value <= to));
        }
        else if (query.FromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc);
            source = source.Where(s => s.CreatedAt >= from || (s.SubmittedAt != null && s.SubmittedAt.Value >= from));
        }
        else if (query.ToDate.HasValue)
        {
            var to = DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc);
            source = source.Where(s => s.CreatedAt <= to || (s.SubmittedAt != null && s.SubmittedAt.Value <= to));
        }
        else if (query.Date.HasValue)
        {
            var startUtc = query.Date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var endUtc = query.Date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            source = source.Where(s =>
                (s.CreatedAt >= startUtc && s.CreatedAt < endUtc) ||
                (s.SubmittedAt != null && s.SubmittedAt.Value >= startUtc && s.SubmittedAt.Value < endUtc));
        }
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
            .ToListAsync(ct);
        return new PagedResult<StudentSubmission> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<List<StudentSubmission>> GetAllSubmittedForStatsAsync(CancellationToken ct = default)
        => Details().Where(s => s.SubmittedAt != null).OrderByDescending(s => s.SubmittedAt).ToListAsync(ct);

    public void Add(StudentSubmission submission) => context.StudentSubmissions.Add(submission);
    public async Task UpdateAsync(StudentSubmission submission, CancellationToken ct = default)
    {
        // Reconcile saved draft rows by question instead of adding duplicate answers.
        var incoming = submission.Answers.ToArray();
        var persisted = await context.StudentSubmissions.FindAsync([submission.Id], ct)
            ?? throw new InvalidOperationException("Submission no longer exists.");
        context.Entry(persisted).CurrentValues.SetValues(submission);
        var answers = await context.StudentAnswers.Where(a => a.SubmissionId == submission.Id).ToListAsync(ct);
        foreach (var answer in incoming)
        {
            var existing = answers.SingleOrDefault(a => a.QuestionId == answer.QuestionId);
            if (existing is null) context.StudentAnswers.Add(answer);
            else
            {
                answer.Id = existing.Id;
                context.Entry(existing).CurrentValues.SetValues(answer);
            }
        }
        var questionIds = incoming.Select(a => a.QuestionId).ToHashSet();
        context.StudentAnswers.RemoveRange(answers.Where(a => !questionIds.Contains(a.QuestionId)));
    }
}
