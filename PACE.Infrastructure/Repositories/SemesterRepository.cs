using Microsoft.EntityFrameworkCore;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class SemesterRepository(AppDbContext context) : GenericRepository<Semester>(context), ISemesterRepository
{
    public async Task<Semester?> GetCurrentSemesterAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Tìm học kỳ mà ngày hiện tại nằm trong [StartDate, EndDate]
        var current = await DbSet
            .Include(s => s.Classes)
            .Where(s => s.StartDate <= today && s.EndDate >= today)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(ct);

        // 2. Nếu không có kỳ nào bao trùm ngày hiện tại, lấy kỳ mới nhất
        current ??= await DbSet
            .Include(s => s.Classes)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(ct);

        return current;
    }

    public Task<List<Semester>> GetAllOrderedAsync(CancellationToken ct = default)
    {
        return DbSet
            .Include(s => s.Classes)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);
    }
}

