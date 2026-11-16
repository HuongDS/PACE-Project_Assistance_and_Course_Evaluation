using Microsoft.EntityFrameworkCore;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class MilestoneRepository(AppDbContext context) : GenericRepository<Milestone>(context), IMilestoneRepository
{
    public async Task<List<Milestone>> GetByClassIdAsync(Guid classId, CancellationToken ct = default)
    {
        return await context.Milestones
            .AsNoTracking()
            .Where(m => m.ClassId == classId)
            .OrderBy(m => m.OrderIndex)
            .ThenBy(m => m.Deadline)
            .ToListAsync(ct);
    }

    public async Task<Milestone?> GetClassMilestoneByTypeAsync(Guid classId, MilestoneType type, CancellationToken ct = default)
    {
        return await context.Milestones
            .FirstOrDefaultAsync(m => m.ClassId == classId && m.Type == type, ct);
    }
}
