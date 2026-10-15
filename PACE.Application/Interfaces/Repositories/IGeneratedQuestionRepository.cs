using PACE.Application.Common.ReadModels;
using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface IGeneratedQuestionRepository : IGenericRepository<GeneratedQuestion>
{
    Task<List<GeneratedQuestionSummary>> GetSummariesByBusinessAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>Xoá câu hỏi của nghiệp vụ chưa có sinh viên nào trả lời. Trả về số dòng đã xoá.</summary>
    Task<int> DeleteUnansweredByBusinessAsync(Guid businessId, CancellationToken ct = default);

    Task<int> CountAnswersAsync(Guid questionId, CancellationToken ct = default);
}
