using PACE.DAL.Models;

namespace PACE.DAL.Repositories.Interfaces;

public interface IRubricCriterionRepository : IGenericRepository<RubricCriterion>
{
    Task<List<RubricCriterion>> GetAllOrderedAsync(CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, Guid? exceptId = null, CancellationToken ct = default);
    Task<bool> IsUsedAsync(Guid id, CancellationToken ct = default);
}
