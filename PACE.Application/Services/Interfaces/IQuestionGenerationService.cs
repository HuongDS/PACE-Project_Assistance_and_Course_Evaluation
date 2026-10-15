using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IQuestionGenerationService
{
    Task<GenerateQuestionsResponse> GenerateAsync(Guid businessId, GenerateQuestionsRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<GeneratedQuestionDto>> GetByBusinessAsync(Guid businessId, CancellationToken ct = default);
    Task DeleteAsync(Guid questionId, CancellationToken ct = default);
}
