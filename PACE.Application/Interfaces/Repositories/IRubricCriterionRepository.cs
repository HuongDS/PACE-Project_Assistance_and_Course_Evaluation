using PACE.Application.Common.Models;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Interfaces.Repositories;

public interface IRubricCriterionRepository : IGenericRepository<RubricCriterion>
{
    Task<List<RubricCriterion>> GetAllOrderedAsync(CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, Guid? exceptId = null, CancellationToken ct = default);
    Task<bool> IsUsedAsync(Guid id, CancellationToken ct = default);
}
