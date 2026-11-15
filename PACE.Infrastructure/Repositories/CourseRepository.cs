using Microsoft.EntityFrameworkCore;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class CourseRepository(AppDbContext context) : GenericRepository<Course>(context), ICourseRepository
{
    public Task<Course?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToLower();
        return DbSet
            .Include(c => c.Classes)
                .ThenInclude(cl => cl.Lecturer)
            .FirstOrDefaultAsync(c => c.Code.ToLower() == normalized, ct);
    }

    public Task<Course?> GetByIdWithLecturerAsync(Guid id, CancellationToken ct = default)
    {
        return DbSet
            .Include(c => c.Classes)
                .ThenInclude(cl => cl.Lecturer)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public Task<List<Course>> GetByLecturerIdAsync(Guid lecturerId, CancellationToken ct = default)
    {
        return DbSet
            .Include(c => c.Classes)
                .ThenInclude(cl => cl.Lecturer)
            .Where(c => c.Classes.Any(cl => cl.LecturerId == lecturerId))
            .OrderBy(c => c.Code)
            .ToListAsync(ct);
    }

    public Task<List<Course>> GetAllWithLecturerAsync(CancellationToken ct = default)
    {
        return DbSet
            .Include(c => c.Classes)
                .ThenInclude(cl => cl.Lecturer)
            .OrderBy(c => c.Code)
            .ToListAsync(ct);
    }
}
