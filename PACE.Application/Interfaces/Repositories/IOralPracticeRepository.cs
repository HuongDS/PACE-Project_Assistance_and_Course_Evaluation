using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface IOralPracticeRepository
{
    // OralExams
    Task<PagedResult<OralExamDto>> SearchOralExamsAsync(OralExamQuery query, CancellationToken ct = default);
    Task<OralExam?> GetOralExamByIdAsync(Guid id, CancellationToken ct = default);
    Task<OralExamDetailDto?> GetOralExamDetailAsync(Guid id, CancellationToken ct = default);
    Task AddOralExamAsync(OralExam exam, CancellationToken ct = default);
    void UpdateOralExam(OralExam exam);
    void RemoveOralExam(OralExam exam);

    // OralQuestions
    Task<PagedResult<OralQuestionDto>> SearchQuestionsAsync(OralQuestionQuery query, CancellationToken ct = default);
    Task<OralQuestion?> GetQuestionByIdAsync(Guid id, CancellationToken ct = default);
    Task AddQuestionAsync(OralQuestion question, CancellationToken ct = default);
    void UpdateQuestion(OralQuestion question);
    void RemoveQuestion(OralQuestion question);

    // StudentOralSessions
    Task<PagedResult<StudentOralSessionDto>> SearchSessionsAsync(StudentOralSessionQuery query, CancellationToken ct = default);
    Task<StudentOralSession?> GetSessionByIdAsync(Guid id, CancellationToken ct = default);
    Task<StudentOralSessionDetailDto?> GetSessionDetailAsync(Guid id, CancellationToken ct = default);
    Task AddSessionAsync(StudentOralSession session, CancellationToken ct = default);
    void UpdateSession(StudentOralSession session);
    void RemoveSession(StudentOralSession session);

    // StudentOralAnswers
    Task<StudentOralAnswer?> GetAnswerByIdAsync(Guid id, CancellationToken ct = default);
    Task<StudentOralAnswer?> GetAnswerBySessionAndQuestionAsync(Guid sessionId, Guid questionId, CancellationToken ct = default);
    Task AddAnswerAsync(StudentOralAnswer answer, CancellationToken ct = default);
    void UpdateAnswer(StudentOralAnswer answer);
}
