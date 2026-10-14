using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class RubricCriterionRepository(AppDbContext context) : GenericRepository<RubricCriterion>(context), IRubricCriterionRepository
{
    public Task<List<RubricCriterion>> GetAllOrderedAsync(CancellationToken ct = default)
        => DbSet.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync(ct);

    public Task<bool> CodeExistsAsync(string code, Guid? exceptId = null, CancellationToken ct = default)
        => DbSet.AnyAsync(c => c.Code == code && c.Id != exceptId, ct);

    public Task<bool> IsUsedAsync(Guid id, CancellationToken ct = default)
        => Context.RubricScores.AnyAsync(s => s.CriterionId == id, ct);
}
