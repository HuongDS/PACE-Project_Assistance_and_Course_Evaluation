using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Interfaces.Repositories;

public interface IMilestoneRepository : IGenericRepository<Milestone>
{
    Task<List<Milestone>> GetByClassIdAsync(Guid classId, CancellationToken ct = default);
    Task<Milestone?> GetClassMilestoneByTypeAsync(Guid classId, MilestoneType type, CancellationToken ct = default);
}
