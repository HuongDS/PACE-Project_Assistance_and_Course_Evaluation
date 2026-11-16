using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class ProfileRepository(AppDbContext context) : IProfileRepository
{
    private static string? NormalizeSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();

    // StudentProfile
    public async Task<PagedResult<StudentProfileDto>> SearchStudentProfilesAsync(StudentProfileQuery query, CancellationToken ct = default)
    {
        var q = context.StudentProfiles.AsNoTracking()
            .Include(s => s.User)
            .AsQueryable();

        if (query.EnrollmentYear.HasValue)
        {
            q = q.Where(s => s.EnrollmentYear == query.EnrollmentYear.Value);
        }

        var search = NormalizeSearch(query.SearchTerm);
        if (!string.IsNullOrEmpty(search))
        {
            q = q.Where(s => s.StudentCode.ToLower().Contains(search)
                          || s.User.FullName.ToLower().Contains(search)
                          || s.User.Email.ToLower().Contains(search));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(s => s.StudentCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StudentProfileDto(
                s.StudentCode,
                s.UserId,
                s.User.FullName,
                s.User.Email,
                s.EnrollmentYear,
                s.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<StudentProfileDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<StudentProfile?> GetStudentProfileByCodeAsync(string studentCode, CancellationToken ct = default)
    {
        return await context.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StudentCode.ToLower() == studentCode.ToLower(), ct);
    }

    public async Task<StudentProfile?> GetStudentProfileByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
    }

    public async Task<bool> StudentCodeExistsAsync(string studentCode, CancellationToken ct = default)
    {
        var normalized = studentCode.Trim().ToLower();
        return await context.StudentProfiles.AnyAsync(s => s.StudentCode.ToLower() == normalized, ct);
    }

    public async Task AddStudentProfileAsync(StudentProfile profile, CancellationToken ct = default)
    {
        await context.StudentProfiles.AddAsync(profile, ct);
    }

    public void UpdateStudentProfile(StudentProfile profile)
    {
        context.StudentProfiles.Update(profile);
    }

    public void RemoveStudentProfile(StudentProfile profile)
    {
        context.StudentProfiles.Remove(profile);
    }

    // LecturerProfile
    public async Task<PagedResult<LecturerProfileDto>> SearchLecturerProfilesAsync(LecturerProfileQuery query, CancellationToken ct = default)
    {
        var q = context.LecturerProfiles.AsNoTracking()
            .Include(l => l.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.AcademicTitle))
        {
            q = q.Where(l => l.AcademicTitle == query.AcademicTitle);
        }

        var search = NormalizeSearch(query.SearchTerm);
        if (!string.IsNullOrEmpty(search))
        {
            q = q.Where(l => l.LecturerCode.ToLower().Contains(search)
                          || l.User.FullName.ToLower().Contains(search)
                          || l.User.Email.ToLower().Contains(search));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(l => l.LecturerCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LecturerProfileDto(
                l.LecturerCode,
                l.UserId,
                l.User.FullName,
                l.User.Email,
                l.AcademicTitle,
                l.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<LecturerProfileDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<LecturerProfile?> GetLecturerProfileByCodeAsync(string lecturerCode, CancellationToken ct = default)
    {
        return await context.LecturerProfiles
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.LecturerCode.ToLower() == lecturerCode.ToLower(), ct);
    }

    public async Task<LecturerProfile?> GetLecturerProfileByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.LecturerProfiles
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.UserId == userId, ct);
    }

    public async Task<bool> LecturerCodeExistsAsync(string lecturerCode, CancellationToken ct = default)
    {
        var normalized = lecturerCode.Trim().ToLower();
        return await context.LecturerProfiles.AnyAsync(l => l.LecturerCode.ToLower() == normalized, ct);
    }

    public async Task AddLecturerProfileAsync(LecturerProfile profile, CancellationToken ct = default)
    {
        await context.LecturerProfiles.AddAsync(profile, ct);
    }

    public void UpdateLecturerProfile(LecturerProfile profile)
    {
        context.LecturerProfiles.Update(profile);
    }

    public void RemoveLecturerProfile(LecturerProfile profile)
    {
        context.LecturerProfiles.Remove(profile);
    }
}
