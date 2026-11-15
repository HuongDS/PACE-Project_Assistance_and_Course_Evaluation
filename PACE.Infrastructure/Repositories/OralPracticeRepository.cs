using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class OralPracticeRepository(AppDbContext context) : IOralPracticeRepository
{
    private static string? NormalizeSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();

    // OralExams
    public async Task<PagedResult<OralExamDto>> SearchOralExamsAsync(OralExamQuery query, CancellationToken ct = default)
    {
        var q = context.OralExams.AsNoTracking()
            .Include(o => o.Class)
            .Include(o => o.Questions)
            .Include(o => o.StudentOralSessions)
            .AsQueryable();

        if (query.ClassId.HasValue) q = q.Where(o => o.ClassId == query.ClassId.Value);

        var search = NormalizeSearch(query.SearchTerm);
        if (!string.IsNullOrEmpty(search))
        {
            q = q.Where(o => o.Title.ToLower().Contains(search) || (o.Description != null && o.Description.ToLower().Contains(search)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OralExamDto(
                o.Id,
                o.ClassId,
                o.Class.Name,
                o.Title,
                o.Description,
                o.CreatedAt,
                o.Questions.Count,
                o.StudentOralSessions.Count))
            .ToListAsync(ct);

        return new PagedResult<OralExamDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<OralExam?> GetOralExamByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.OralExams.FindAsync([id], ct);
    }

    public async Task<OralExamDetailDto?> GetOralExamDetailAsync(Guid id, CancellationToken ct = default)
    {
        var exam = await context.OralExams.AsNoTracking()
            .Where(o => o.Id == id)
            .Include(o => o.Class)
            .Include(o => o.Questions)
            .FirstOrDefaultAsync(ct);

        if (exam == null) return null;

        var questions = exam.Questions
            .Select(q => new OralQuestionDto(
                q.Id,
                q.OralExamId,
                q.GeneratedQuestionId,
                q.Content,
                q.ReferenceContext,
                q.ExpectedKeywords))
            .ToList();

        return new OralExamDetailDto(
            exam.Id,
            exam.ClassId,
            exam.Class?.Name,
            exam.Title,
            exam.Description,
            exam.CreatedAt,
            questions);
    }

    public async Task AddOralExamAsync(OralExam exam, CancellationToken ct = default)
    {
        await context.OralExams.AddAsync(exam, ct);
    }

    public void UpdateOralExam(OralExam exam)
    {
        context.OralExams.Update(exam);
    }

    public void RemoveOralExam(OralExam exam)
    {
        context.OralExams.Remove(exam);
    }

    // OralQuestions
    public async Task<PagedResult<OralQuestionDto>> SearchQuestionsAsync(OralQuestionQuery query, CancellationToken ct = default)
    {
        var q = context.OralQuestions.AsNoTracking().AsQueryable();

        if (query.OralExamId.HasValue) q = q.Where(q => q.OralExamId == query.OralExamId.Value);

        var search = NormalizeSearch(query.SearchTerm);
        if (!string.IsNullOrEmpty(search))
        {
            q = q.Where(q => q.Content.ToLower().Contains(search)
                          || (q.ExpectedKeywords != null && q.ExpectedKeywords.ToLower().Contains(search)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(q => q.Content)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new OralQuestionDto(
                q.Id,
                q.OralExamId,
                q.GeneratedQuestionId,
                q.Content,
                q.ReferenceContext,
                q.ExpectedKeywords))
            .ToListAsync(ct);

        return new PagedResult<OralQuestionDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<OralQuestion?> GetQuestionByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.OralQuestions.FindAsync([id], ct);
    }

    public async Task AddQuestionAsync(OralQuestion question, CancellationToken ct = default)
    {
        await context.OralQuestions.AddAsync(question, ct);
    }

    public void UpdateQuestion(OralQuestion question)
    {
        context.OralQuestions.Update(question);
    }

    public void RemoveQuestion(OralQuestion question)
    {
        context.OralQuestions.Remove(question);
    }

    // StudentOralSessions
    public async Task<PagedResult<StudentOralSessionDto>> SearchSessionsAsync(StudentOralSessionQuery query, CancellationToken ct = default)
    {
        var q = context.StudentOralSessions.AsNoTracking()
            .Include(s => s.OralExam)
            .Include(s => s.StudentProfile).ThenInclude(sp => sp.User)
            .Include(s => s.Examiner!).ThenInclude(e => e.User)
            .Include(s => s.Answers)
            .AsQueryable();

        if (query.OralExamId.HasValue) q = q.Where(s => s.OralExamId == query.OralExamId.Value);
        if (!string.IsNullOrEmpty(query.StudentCode)) q = q.Where(s => s.StudentCode == query.StudentCode);
        if (!string.IsNullOrEmpty(query.ExaminerCode)) q = q.Where(s => s.ExaminerCode == query.ExaminerCode);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderByDescending(s => s.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentOralSessionDto(
                s.Id,
                s.OralExamId,
                s.OralExam.Title,
                s.StudentCode,
                s.StudentProfile.User.FullName,
                s.ExaminerCode,
                s.Examiner != null ? s.Examiner.User.FullName : null,
                s.StartTime,
                s.EndTime,
                s.AudioRecordUrl,
                s.ScreenRecordUrl,
                s.AiEvaluationSummary,
                s.FinalFeedback,
                s.Answers.Count))
            .ToListAsync(ct);

        return new PagedResult<StudentOralSessionDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<StudentOralSession?> GetSessionByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.StudentOralSessions.FindAsync([id], ct);
    }

    public async Task<StudentOralSessionDetailDto?> GetSessionDetailAsync(Guid id, CancellationToken ct = default)
    {
        var session = await context.StudentOralSessions.AsNoTracking()
            .Where(s => s.Id == id)
            .Include(s => s.OralExam)
            .Include(s => s.StudentProfile).ThenInclude(sp => sp.User)
            .Include(s => s.Examiner!).ThenInclude(e => e.User)
            .Include(s => s.Answers).ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(ct);

        if (session == null) return null;

        var answers = session.Answers
            .Select(a => new StudentOralAnswerDto(
                a.Id,
                a.SessionId,
                a.QuestionId,
                a.Question != null ? a.Question.Content : null,
                a.Question != null ? a.Question.ExpectedKeywords : null,
                a.StudentAnswer,
                a.Feedback))
            .ToList();

        return new StudentOralSessionDetailDto(
            session.Id,
            session.OralExamId,
            session.OralExam?.Title,
            session.StudentCode,
            session.StudentProfile?.User?.FullName,
            session.ExaminerCode,
            session.Examiner?.User?.FullName,
            session.StartTime,
            session.EndTime,
            session.AudioRecordUrl,
            session.ScreenRecordUrl,
            session.AiEvaluationSummary,
            session.FinalFeedback,
            answers);
    }

    public async Task AddSessionAsync(StudentOralSession session, CancellationToken ct = default)
    {
        await context.StudentOralSessions.AddAsync(session, ct);
    }

    public void UpdateSession(StudentOralSession session)
    {
        context.StudentOralSessions.Update(session);
    }

    public void RemoveSession(StudentOralSession session)
    {
        context.StudentOralSessions.Remove(session);
    }

    // StudentOralAnswers
    public async Task<StudentOralAnswer?> GetAnswerByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.StudentOralAnswers.FindAsync([id], ct);
    }

    public async Task<StudentOralAnswer?> GetAnswerBySessionAndQuestionAsync(Guid sessionId, Guid questionId, CancellationToken ct = default)
    {
        return await context.StudentOralAnswers
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.QuestionId == questionId, ct);
    }

    public async Task AddAnswerAsync(StudentOralAnswer answer, CancellationToken ct = default)
    {
        await context.StudentOralAnswers.AddAsync(answer, ct);
    }

    public void UpdateAnswer(StudentOralAnswer answer)
    {
        context.StudentOralAnswers.Update(answer);
    }
}
