using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IRubricCriterionService
{
    Task<IReadOnlyList<RubricCriterionDto>> GetAllAsync(CancellationToken ct = default);
    Task<RubricCriterionDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RubricCriterionDto> CreateAsync(UpsertRubricCriterionRequest request, CancellationToken ct = default);
    Task<RubricCriterionDto> UpdateAsync(Guid id, UpsertRubricCriterionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

