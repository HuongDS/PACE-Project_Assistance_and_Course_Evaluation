using Microsoft.EntityFrameworkCore;
using PACE.DAL.Data;
using PACE.DAL.Models;
using PACE.DAL.Repositories.Interfaces;

namespace PACE.DAL.Repositories.Implementations;

public class RubricCriterionRepository(AppDbContext context) : GenericRepository<RubricCriterion>(context), IRubricCriterionRepository
{
    public Task<List<RubricCriterion>> GetAllOrderedAsync(CancellationToken ct = default)
        => DbSet.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync(ct);

    public Task<bool> CodeExistsAsync(string code, Guid? exceptId = null, CancellationToken ct = default)
        => DbSet.AnyAsync(c => c.Code == code && c.Id != exceptId, ct);

    public Task<bool> IsUsedAsync(Guid id, CancellationToken ct = default)
        => Context.RubricScores.AnyAsync(s => s.CriterionId == id, ct);
}
