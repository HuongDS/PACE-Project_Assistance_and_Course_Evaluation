using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface ISemesterRepository : IGenericRepository<Semester>
{
    Task<Semester?> GetCurrentSemesterAsync(CancellationToken ct = default);

    Task<List<Semester>> GetAllOrderedAsync(CancellationToken ct = default);
}

