using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class OralPracticeService(
    IUnitOfWork uow,
    ICurrentUserService currentUser) : IOralPracticeService
{
    private async Task EnsureStaffAccessAsync(Guid classId, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        throw new ForbiddenException("Bạn không có quyền quản trị kỳ vấn đáp này.");
    }

    // OralExam
    public async Task<PagedResult<OralExamDto>> GetOralExamsPagedAsync(OralExamQuery query, CancellationToken ct = default)
    {
        return await uow.OralPractices.SearchOralExamsAsync(query, ct);
    }

    public async Task<OralExamDetailDto> GetOralExamDetailAsync(Guid id, CancellationToken ct = default)
    {
        var exam = await uow.OralPractices.GetOralExamDetailAsync(id, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");
        return exam;
    }

    public async Task<OralExamDto> CreateOralExamAsync(CreateOralExamRequest request, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(request.ClassId, ct);

        var classExists = await uow.Classes.ClassExistsAsync(request.ClassId, ct);
        if (!classExists)
            throw new NotFoundException("Lớp học không tồn tại.");

        var exam = new OralExam
        {
            Id = Guid.NewGuid(),
            ClassId = request.ClassId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await uow.OralPractices.AddOralExamAsync(exam, ct);
        await uow.SaveChangesAsync(ct);

        var classDetail = await uow.Classes.GetByIdAsync(exam.ClassId, ct);

        return new OralExamDto(
            exam.Id,
            exam.ClassId,
            classDetail?.Name,
            exam.Title,
            exam.Description,
            exam.CreatedAt,
            0,
            0);
    }

    public async Task<OralExamDto> UpdateOralExamAsync(Guid id, UpdateOralExamRequest request, CancellationToken ct = default)
    {
        var exam = await uow.OralPractices.GetOralExamByIdAsync(id, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");

        await EnsureStaffAccessAsync(exam.ClassId, ct);

        exam.Title = request.Title.Trim();
        exam.Description = request.Description?.Trim();

        uow.OralPractices.UpdateOralExam(exam);
        await uow.SaveChangesAsync(ct);

        var detail = await uow.OralPractices.GetOralExamDetailAsync(id, ct);
        var sessionCount = (await uow.OralPractices.SearchSessionsAsync(new StudentOralSessionQuery { OralExamId = id }, ct)).TotalCount;

        return new OralExamDto(
            exam.Id,
            exam.ClassId,
            detail?.ClassName,
            exam.Title,
            exam.Description,
            exam.CreatedAt,
            detail?.Questions.Count ?? 0,
            sessionCount);
    }

    public async Task DeleteOralExamAsync(Guid id, CancellationToken ct = default)
    {
        var exam = await uow.OralPractices.GetOralExamByIdAsync(id, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");

        await EnsureStaffAccessAsync(exam.ClassId, ct);

        uow.OralPractices.RemoveOralExam(exam);
        await uow.SaveChangesAsync(ct);
    }

    // OralQuestion
    public async Task<PagedResult<OralQuestionDto>> GetQuestionsPagedAsync(OralQuestionQuery query, CancellationToken ct = default)
    {
        return await uow.OralPractices.SearchQuestionsAsync(query, ct);
    }

    public async Task<OralQuestionDto> GetQuestionByIdAsync(Guid id, CancellationToken ct = default)
    {
        var q = await uow.OralPractices.GetQuestionByIdAsync(id, ct)
            ?? throw new NotFoundException("Câu hỏi vấn đáp không tồn tại.");

        return new OralQuestionDto(
            q.Id,
            q.OralExamId,
            q.GeneratedQuestionId,
            q.Content,
            q.ReferenceContext,
            q.ExpectedKeywords);
    }

    public async Task<OralQuestionDto> CreateQuestionAsync(CreateOralQuestionRequest request, CancellationToken ct = default)
    {
        var exam = await uow.OralPractices.GetOralExamByIdAsync(request.OralExamId, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");

        await EnsureStaffAccessAsync(exam.ClassId, ct);

        var question = new OralQuestion
        {
            Id = Guid.NewGuid(),
            OralExamId = request.OralExamId,
            GeneratedQuestionId = request.GeneratedQuestionId,
            Content = request.Content.Trim(),
            ReferenceContext = request.ReferenceContext?.Trim(),
            ExpectedKeywords = request.ExpectedKeywords?.Trim()
        };

        await uow.OralPractices.AddQuestionAsync(question, ct);
        await uow.SaveChangesAsync(ct);

        return new OralQuestionDto(
            question.Id,
            question.OralExamId,
            question.GeneratedQuestionId,
            question.Content,
            question.ReferenceContext,
            question.ExpectedKeywords);
    }

    public async Task<OralQuestionDto> UpdateQuestionAsync(Guid id, UpdateOralQuestionRequest request, CancellationToken ct = default)
    {
        var question = await uow.OralPractices.GetQuestionByIdAsync(id, ct)
            ?? throw new NotFoundException("Câu hỏi vấn đáp không tồn tại.");

        var exam = await uow.OralPractices.GetOralExamByIdAsync(question.OralExamId, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");

        await EnsureStaffAccessAsync(exam.ClassId, ct);

        question.Content = request.Content.Trim();
        question.ReferenceContext = request.ReferenceContext?.Trim();
        question.ExpectedKeywords = request.ExpectedKeywords?.Trim();

        uow.OralPractices.UpdateQuestion(question);
        await uow.SaveChangesAsync(ct);

        return new OralQuestionDto(
            question.Id,
            question.OralExamId,
            question.GeneratedQuestionId,
            question.Content,
            question.ReferenceContext,
            question.ExpectedKeywords);
    }

    public async Task DeleteQuestionAsync(Guid id, CancellationToken ct = default)
    {
        var question = await uow.OralPractices.GetQuestionByIdAsync(id, ct)
            ?? throw new NotFoundException("Câu hỏi vấn đáp không tồn tại.");

        var exam = await uow.OralPractices.GetOralExamByIdAsync(question.OralExamId, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");

        await EnsureStaffAccessAsync(exam.ClassId, ct);

        uow.OralPractices.RemoveQuestion(question);
        await uow.SaveChangesAsync(ct);
    }

    // StudentOralSession
    public async Task<PagedResult<StudentOralSessionDto>> GetSessionsPagedAsync(StudentOralSessionQuery query, CancellationToken ct = default)
    {
        if (currentUser.Role == UserRole.Student)
        {
            var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            query.StudentCode = studentCode;
        }

        return await uow.OralPractices.SearchSessionsAsync(query, ct);
    }

    public async Task<StudentOralSessionDetailDto> GetSessionDetailAsync(Guid id, CancellationToken ct = default)
    {
        var session = await uow.OralPractices.GetSessionDetailAsync(id, ct)
            ?? throw new NotFoundException("Phiên vấn đáp của sinh viên không tồn tại.");

        if (currentUser.Role == UserRole.Student)
        {
            var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            if (session.StudentCode != studentCode)
                throw new ForbiddenException("Bạn chỉ có thể xem phiên vấn đáp của chính mình.");
        }

        return session;
    }

    public async Task<StudentOralSessionDto> StartSessionAsync(StartStudentOralSessionRequest request, CancellationToken ct = default)
    {
        var exam = await uow.OralPractices.GetOralExamByIdAsync(request.OralExamId, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");

        if (currentUser.Role == UserRole.Student)
        {
            var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            if (request.StudentCode.Trim().ToLower() != studentCode?.ToLower())
                throw new ForbiddenException("Sinh viên chỉ có thể bắt đầu phiên thi của chính mình.");
        }
        else
        {
            await EnsureStaffAccessAsync(exam.ClassId, ct);
        }

        var studentProfile = await uow.Profiles.GetStudentProfileByCodeAsync(request.StudentCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ sinh viên với mã '{request.StudentCode}'.");

        var session = new StudentOralSession
        {
            Id = Guid.NewGuid(),
            OralExamId = request.OralExamId,
            StudentCode = studentProfile.StudentCode,
            ExaminerCode = request.ExaminerCode?.Trim(),
            StartTime = request.StartTime ?? DateTime.UtcNow
        };

        await uow.OralPractices.AddSessionAsync(session, ct);
        await uow.SaveChangesAsync(ct);

        var examiner = !string.IsNullOrEmpty(session.ExaminerCode)
            ? await uow.Profiles.GetLecturerProfileByCodeAsync(session.ExaminerCode, ct)
            : null;

        return new StudentOralSessionDto(
            session.Id,
            session.OralExamId,
            exam.Title,
            session.StudentCode,
            studentProfile.User?.FullName,
            session.ExaminerCode,
            examiner?.User?.FullName,
            session.StartTime,
            session.EndTime,
            session.AudioRecordUrl,
            session.ScreenRecordUrl,
            session.AiEvaluationSummary,
            session.FinalFeedback,
            0);
    }

    public async Task<StudentOralSessionDto> UpdateSessionAsync(Guid id, UpdateStudentOralSessionRequest request, CancellationToken ct = default)
    {
        var session = await uow.OralPractices.GetSessionByIdAsync(id, ct)
            ?? throw new NotFoundException("Phiên vấn đáp không tồn tại.");

        var exam = await uow.OralPractices.GetOralExamByIdAsync(session.OralExamId, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");

        if (currentUser.Role == UserRole.Student)
        {
            var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            if (session.StudentCode != studentCode)
                throw new ForbiddenException("Bạn chỉ có thể cập nhật phiên vấn đáp của chính mình.");

            // Sinh viên chỉ được nộp URL ghi âm / quay màn hình hoặc kết thúc thời gian
            session.EndTime = request.EndTime ?? session.EndTime ?? DateTime.UtcNow;
            if (!string.IsNullOrEmpty(request.AudioRecordUrl)) session.AudioRecordUrl = request.AudioRecordUrl;
            if (!string.IsNullOrEmpty(request.ScreenRecordUrl)) session.ScreenRecordUrl = request.ScreenRecordUrl;
        }
        else
        {
            await EnsureStaffAccessAsync(exam.ClassId, ct);

            if (request.EndTime.HasValue) session.EndTime = request.EndTime.Value;
            if (request.AudioRecordUrl != null) session.AudioRecordUrl = request.AudioRecordUrl;
            if (request.ScreenRecordUrl != null) session.ScreenRecordUrl = request.ScreenRecordUrl;
            if (request.AiEvaluationSummary != null) session.AiEvaluationSummary = request.AiEvaluationSummary;
            if (request.FinalFeedback != null) session.FinalFeedback = request.FinalFeedback;
        }

        uow.OralPractices.UpdateSession(session);
        await uow.SaveChangesAsync(ct);

        var detail = await uow.OralPractices.GetSessionDetailAsync(id, ct);

        return new StudentOralSessionDto(
            session.Id,
            session.OralExamId,
            exam.Title,
            session.StudentCode,
            detail?.StudentName,
            session.ExaminerCode,
            detail?.ExaminerName,
            session.StartTime,
            session.EndTime,
            session.AudioRecordUrl,
            session.ScreenRecordUrl,
            session.AiEvaluationSummary,
            session.FinalFeedback,
            detail?.Answers.Count ?? 0);
    }

    public async Task DeleteSessionAsync(Guid id, CancellationToken ct = default)
    {
        var session = await uow.OralPractices.GetSessionByIdAsync(id, ct)
            ?? throw new NotFoundException("Phiên vấn đáp không tồn tại.");

        var exam = await uow.OralPractices.GetOralExamByIdAsync(session.OralExamId, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");

        await EnsureStaffAccessAsync(exam.ClassId, ct);

        uow.OralPractices.RemoveSession(session);
        await uow.SaveChangesAsync(ct);
    }

    // StudentOralAnswer
    public async Task<StudentOralAnswerDto> SubmitAnswerAsync(Guid sessionId, SubmitOralAnswerRequest request, CancellationToken ct = default)
    {
        var session = await uow.OralPractices.GetSessionByIdAsync(sessionId, ct)
            ?? throw new NotFoundException("Phiên vấn đáp không tồn tại.");

        var exam = await uow.OralPractices.GetOralExamByIdAsync(session.OralExamId, ct)
            ?? throw new NotFoundException("Kỳ vấn đáp không tồn tại.");

        var question = await uow.OralPractices.GetQuestionByIdAsync(request.QuestionId, ct)
            ?? throw new NotFoundException("Câu hỏi vấn đáp không tồn tại.");

        if (currentUser.Role == UserRole.Student)
        {
            var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            if (session.StudentCode != studentCode)
                throw new ForbiddenException("Bạn không thể nộp câu trả lời cho phiên thi của sinh viên khác.");
        }

        var existingAnswer = await uow.OralPractices.GetAnswerBySessionAndQuestionAsync(sessionId, request.QuestionId, ct);
        if (existingAnswer != null)
        {
            existingAnswer.StudentAnswer = request.StudentAnswer?.Trim();
            if (currentUser.IsAdmin || currentUser.Role == UserRole.Lecturer)
            {
                existingAnswer.Feedback = request.Feedback?.Trim();
            }
            uow.OralPractices.UpdateAnswer(existingAnswer);
            await uow.SaveChangesAsync(ct);

            return new StudentOralAnswerDto(
                existingAnswer.Id,
                existingAnswer.SessionId,
                existingAnswer.QuestionId,
                question.Content,
                question.ExpectedKeywords,
                existingAnswer.StudentAnswer,
                existingAnswer.Feedback);
        }

        var answer = new StudentOralAnswer
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            QuestionId = request.QuestionId,
            StudentAnswer = request.StudentAnswer?.Trim(),
            Feedback = (currentUser.IsAdmin || currentUser.Role == UserRole.Lecturer) ? request.Feedback?.Trim() : null
        };

        await uow.OralPractices.AddAnswerAsync(answer, ct);
        await uow.SaveChangesAsync(ct);

        return new StudentOralAnswerDto(
            answer.Id,
            answer.SessionId,
            answer.QuestionId,
            question.Content,
            question.ExpectedKeywords,
            answer.StudentAnswer,
            answer.Feedback);
    }

    public async Task<StudentOralAnswerDto> UpdateAnswerAsync(Guid sessionId, Guid answerId, UpdateOralAnswerRequest request, CancellationToken ct = default)
    {
        var answer = await uow.OralPractices.GetAnswerByIdAsync(answerId, ct)
            ?? throw new NotFoundException("Câu trả lời không tồn tại.");

        if (answer.SessionId != sessionId)
            throw new BadRequestException("Câu trả lời không thuộc phiên thi này.");

        var session = await uow.OralPractices.GetSessionByIdAsync(sessionId, ct)
            ?? throw new NotFoundException("Phiên vấn đáp không tồn tại.");

        var question = await uow.OralPractices.GetQuestionByIdAsync(answer.QuestionId, ct)
            ?? throw new NotFoundException("Câu hỏi không tồn tại.");

        if (currentUser.Role == UserRole.Student)
        {
            var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            if (session.StudentCode != studentCode)
                throw new ForbiddenException("Bạn không thể chỉnh sửa câu trả lời của phiên thi khác.");

            answer.StudentAnswer = request.StudentAnswer?.Trim();
        }
        else
        {
            if (request.StudentAnswer != null) answer.StudentAnswer = request.StudentAnswer.Trim();
            if (request.Feedback != null) answer.Feedback = request.Feedback.Trim();
        }

        uow.OralPractices.UpdateAnswer(answer);
        await uow.SaveChangesAsync(ct);

        return new StudentOralAnswerDto(
            answer.Id,
            answer.SessionId,
            answer.QuestionId,
            question.Content,
            question.ExpectedKeywords,
            answer.StudentAnswer,
            answer.Feedback);
    }
}
