using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class AttendanceRepository(AppDbContext context) : GenericRepository<Attendance>(context), IAttendanceRepository
{
    public async Task<PagedResult<AttendanceDto>> SearchAttendanceAsync(AttendanceQuery query, CancellationToken ct = default)
    {
        var q = DbSet.AsNoTracking()
            .Include(a => a.StudentProfile).ThenInclude(s => s.User)
            .AsQueryable();

        if (query.SessionId.HasValue) q = q.Where(a => a.SessionId == query.SessionId.Value);
        if (!string.IsNullOrEmpty(query.StudentCode)) q = q.Where(a => a.StudentCode == query.StudentCode);
        if (query.Status.HasValue) q = q.Where(a => a.Status == query.Status.Value);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(a => a.StudentCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AttendanceDto(
                a.Id,
                a.SessionId,
                a.StudentCode,
                a.StudentProfile.UserId,
                a.StudentProfile.User.FullName,
                a.StudentProfile.User.Email,
                a.Status,
                a.RecordedAt,
                a.Comments))
            .ToListAsync(ct);

        return new PagedResult<AttendanceDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<List<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .Include(a => a.StudentProfile).ThenInclude(s => s.User)
            .OrderBy(a => a.StudentCode)
            .Select(a => new AttendanceDto(
                a.Id,
                a.SessionId,
                a.StudentCode,
                a.StudentProfile.UserId,
                a.StudentProfile.User.FullName,
                a.StudentProfile.User.Email,
                a.Status,
                a.RecordedAt,
                a.Comments))
            .ToListAsync(ct);
    }

    public async Task<Attendance?> GetBySessionAndStudentAsync(Guid sessionId, string studentCode, CancellationToken ct = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(a => a.SessionId == sessionId && a.StudentCode.ToLower() == studentCode.ToLower(), ct);
    }
}
