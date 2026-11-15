using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IOralPracticeService
{
    // OralExam
    Task<PagedResult<OralExamDto>> GetOralExamsPagedAsync(OralExamQuery query, CancellationToken ct = default);
    Task<OralExamDetailDto> GetOralExamDetailAsync(Guid id, CancellationToken ct = default);
    Task<OralExamDto> CreateOralExamAsync(CreateOralExamRequest request, CancellationToken ct = default);
    Task<OralExamDto> UpdateOralExamAsync(Guid id, UpdateOralExamRequest request, CancellationToken ct = default);
    Task DeleteOralExamAsync(Guid id, CancellationToken ct = default);

    // OralQuestion
    Task<PagedResult<OralQuestionDto>> GetQuestionsPagedAsync(OralQuestionQuery query, CancellationToken ct = default);
    Task<OralQuestionDto> GetQuestionByIdAsync(Guid id, CancellationToken ct = default);
    Task<OralQuestionDto> CreateQuestionAsync(CreateOralQuestionRequest request, CancellationToken ct = default);
    Task<OralQuestionDto> UpdateQuestionAsync(Guid id, UpdateOralQuestionRequest request, CancellationToken ct = default);
    Task DeleteQuestionAsync(Guid id, CancellationToken ct = default);

    // StudentOralSession
    Task<PagedResult<StudentOralSessionDto>> GetSessionsPagedAsync(StudentOralSessionQuery query, CancellationToken ct = default);
    Task<StudentOralSessionDetailDto> GetSessionDetailAsync(Guid id, CancellationToken ct = default);
    Task<StudentOralSessionDto> StartSessionAsync(StartStudentOralSessionRequest request, CancellationToken ct = default);
    Task<StudentOralSessionDto> UpdateSessionAsync(Guid id, UpdateStudentOralSessionRequest request, CancellationToken ct = default);
    Task DeleteSessionAsync(Guid id, CancellationToken ct = default);

    // StudentOralAnswer
    Task<StudentOralAnswerDto> SubmitAnswerAsync(Guid sessionId, SubmitOralAnswerRequest request, CancellationToken ct = default);
    Task<StudentOralAnswerDto> UpdateAnswerAsync(Guid sessionId, Guid answerId, UpdateOralAnswerRequest request, CancellationToken ct = default);
}
