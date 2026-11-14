using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface ICourseRepository : IGenericRepository<Course>
{
    Task<Course?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<Course?> GetByIdWithLecturerAsync(Guid id, CancellationToken ct = default);

    Task<List<Course>> GetByLecturerIdAsync(Guid lecturerId, CancellationToken ct = default);

    Task<List<Course>> GetAllWithLecturerAsync(CancellationToken ct = default);
}
