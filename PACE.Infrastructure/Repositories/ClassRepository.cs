using Microsoft.EntityFrameworkCore;
using PACE.Application.Common;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class ClassRepository(AppDbContext context) : GenericRepository<Class>(context), IClassRepository
{
    public async Task<PagedResult<ClassDto>> SearchClassesAsync(
        ClassQuery query,
        Guid? lecturerUserId = null,
        string? studentCode = null,
        CancellationToken ct = default)
    {
        var q = DbSet.AsNoTracking();

        if (lecturerUserId.HasValue)
        {
            var lecturerCode = await Context.LecturerProfiles
                .Where(l => l.UserId == lecturerUserId.Value)
                .Select(l => l.LecturerCode)
                .FirstOrDefaultAsync(ct);

            q = q.Where(c => c.Course.LecturerId == lecturerUserId.Value
                          || (!string.IsNullOrEmpty(lecturerCode) && (c.Schedules.Any(s => s.LecturerCode == lecturerCode)
                                                                   || c.Sessions.Any(s => s.LecturerCode == lecturerCode))));
        }
        else if (!string.IsNullOrEmpty(studentCode))
        {
            q = q.Where(c => c.StudentClasses.Any(sc => sc.StudentCode == studentCode));
        }

        if (query.CourseId.HasValue)
        {
            q = q.Where(c => c.CourseId == query.CourseId.Value);
        }

        var keyword = NormalizeSearch(query.SearchTerm);
        if (keyword != null)
        {
            q = q.Where(c =>
                (c.Name != null && c.Name.ToLower().Contains(keyword)) ||
                c.Course.Code.ToLower().Contains(keyword) ||
                c.Course.Name.ToLower().Contains(keyword) ||
                (c.Course.Semester != null && c.Course.Semester.ToLower().Contains(keyword)) ||
                (c.Semester != null && c.Semester.Name.ToLower().Contains(keyword)) ||
                (c.Course.Lecturer != null && (c.Course.Lecturer.FullName.ToLower().Contains(keyword) || c.Course.Lecturer.Email.ToLower().Contains(keyword))));
        }

        var projectQuery = q.OrderByDescending(c => c.StartDate).ThenBy(c => c.Name)
            .Select(c => new ClassDto(
                c.Id,
                c.Name,
                c.CourseId,
                c.Course.Code,
                c.Course.Name,
                c.Course.LecturerId,
                c.Course.Lecturer != null && !string.IsNullOrEmpty(c.Course.Lecturer.FullName) ? c.Course.Lecturer.FullName : (c.Course.Lecturer != null ? c.Course.Lecturer.Email : null),
                c.Course.Lecturer != null ? c.Course.Lecturer.Email : null,
                c.Course.Semester ?? (c.Semester != null ? c.Semester.Name : null),
                c.StartDate,
                c.EndDate,
                c.StudentClasses.Count,
                c.Groups.Count));

        return await ToPagedAsync(projectQuery, query.Page, query.PageSize, ct);
    }

    public async Task<ClassDetailDto?> GetClassDetailAsync(Guid classId, CancellationToken ct = default)
    {
        var c = await DbSet.AsNoTracking()
            .Include(c => c.Course)
                .ThenInclude(co => co.Lecturer)
            .Include(c => c.Semester)
            .FirstOrDefaultAsync(c => c.Id == classId, ct);

        if (c == null) return null;

        var classDto = new ClassDto(
            c.Id,
            c.Name,
            c.CourseId,
            c.Course.Code,
            c.Course.Name,
            c.Course.LecturerId,
            c.Course.Lecturer != null && !string.IsNullOrEmpty(c.Course.Lecturer.FullName) ? c.Course.Lecturer.FullName : (c.Course.Lecturer != null ? c.Course.Lecturer.Email : null),
            c.Course.Lecturer != null ? c.Course.Lecturer.Email : null,
            c.Course.Semester ?? c.Semester?.Name,
            c.StartDate,
            c.EndDate,
            await Context.StudentClasses.CountAsync(sc => sc.ClassId == classId, ct),
            await Context.Groups.CountAsync(g => g.ClassId == classId, ct));

        // Get students in this class with their Group name if assigned
        var students = await Context.StudentClasses.AsNoTracking()
            .Where(sc => sc.ClassId == classId)
            .Select(sc => new ClassStudentDto(
                sc.StudentCode,
                sc.StudentProfile != null ? (Guid?)sc.StudentProfile.UserId : null,
                sc.StudentProfile != null ? sc.StudentProfile.User.FullName : null,
                sc.StudentProfile != null ? sc.StudentProfile.User.Email : null,
                sc.Status,
                Context.GroupMembers
                    .Where(gm => gm.StudentCode == sc.StudentCode && gm.Group.ClassId == classId)
                    .Select(gm => gm.Group.Name)
                    .FirstOrDefault(),
                sc.StudentProfile != null ? (DateTime?)sc.StudentProfile.CreatedAt : null))
            .OrderBy(s => s.StudentCode)
            .ToListAsync(ct);

        // Get groups in this class
        var groups = await Context.Groups.AsNoTracking()
            .Where(g => g.ClassId == classId)
            .Select(g => new ClassGroupDto(
                g.Id,
                g.Name,
                g.TopicName,
                g.AnalysisRuns.Select(r => (Guid?)r.Id).FirstOrDefault(),
                g.AnalysisRuns.Select(r => r.RepoName).FirstOrDefault(),
                g.AnalysisRuns.Select(r => r.RepoUrl).FirstOrDefault(),
                g.Members.Count))
            .OrderBy(g => g.Name)
            .ToListAsync(ct);

        return new ClassDetailDto(classDto, students, groups);
    }

    public async Task<PagedResult<ClassStudentDto>> GetClassStudentsAsync(
        Guid classId,
        ClassStudentQuery query,
        CancellationToken ct = default)
    {
        var q = Context.StudentClasses.AsNoTracking()
            .Where(sc => sc.ClassId == classId);

        var keyword = NormalizeSearch(query.SearchTerm);
        if (keyword != null)
        {
            q = q.Where(sc =>
                sc.StudentCode.ToLower().Contains(keyword) ||
                (sc.StudentProfile != null && (
                    sc.StudentProfile.User.Email.ToLower().Contains(keyword) ||
                    sc.StudentProfile.User.FullName.ToLower().Contains(keyword)
                )) ||
                Context.GroupMembers.Any(gm => gm.StudentCode == sc.StudentCode && gm.Group.ClassId == classId && gm.Group.Name.ToLower().Contains(keyword)));
        }

        var projectQuery = q.OrderBy(sc => sc.StudentCode)
            .Select(sc => new ClassStudentDto(
                sc.StudentCode,
                sc.StudentProfile != null ? (Guid?)sc.StudentProfile.UserId : null,
                sc.StudentProfile != null ? sc.StudentProfile.User.FullName : null,
                sc.StudentProfile != null ? sc.StudentProfile.User.Email : null,
                sc.Status,
                Context.GroupMembers
                    .Where(gm => gm.StudentCode == sc.StudentCode && gm.Group.ClassId == classId)
                    .Select(gm => gm.Group.Name)
                    .FirstOrDefault(),
                sc.StudentProfile != null ? (DateTime?)sc.StudentProfile.CreatedAt : null));

        return await ToPagedAsync(projectQuery, query.Page, query.PageSize, ct);
    }

    public Task<List<ClassStudentDto>> GetAllStudentsInClassAsync(Guid classId, CancellationToken ct = default)
    {
        return Context.StudentClasses.AsNoTracking()
            .Where(sc => sc.ClassId == classId)
            .Select(sc => new ClassStudentDto(
                sc.StudentCode,
                sc.StudentProfile != null ? (Guid?)sc.StudentProfile.UserId : null,
                sc.StudentProfile != null ? sc.StudentProfile.User.FullName : null,
                sc.StudentProfile != null ? sc.StudentProfile.User.Email : null,
                sc.Status,
                Context.GroupMembers
                    .Where(gm => gm.StudentCode == sc.StudentCode && gm.Group.ClassId == classId)
                    .Select(gm => gm.Group.Name)
                    .FirstOrDefault(),
                sc.StudentProfile != null ? (DateTime?)sc.StudentProfile.CreatedAt : null))
            .OrderBy(s => s.StudentCode)
            .ToListAsync(ct);
    }

    public async Task<bool> IsLecturerOfClassAsync(Guid classId, Guid lecturerUserId, CancellationToken ct = default)
    {
        var lecturerCode = await Context.LecturerProfiles
            .Where(l => l.UserId == lecturerUserId)
            .Select(l => l.LecturerCode)
            .FirstOrDefaultAsync(ct);

        return await Context.Classes
            .Where(c => c.Id == classId)
            .AnyAsync(c => c.Course.LecturerId == lecturerUserId
                        || (!string.IsNullOrEmpty(lecturerCode) && (c.Schedules.Any(s => s.LecturerCode == lecturerCode)
                                                                 || c.Sessions.Any(s => s.LecturerCode == lecturerCode))), ct);
    }

    public async Task<bool> IsStudentOfClassAsync(Guid classId, Guid studentUserId, string? studentCode = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(studentCode))
        {
            studentCode = await GetStudentCodeByUserIdAsync(studentUserId, ct);
        }

        if (string.IsNullOrEmpty(studentCode)) return false;

        return await Context.StudentClasses
            .AnyAsync(sc => sc.ClassId == classId && sc.StudentCode == studentCode, ct);
    }

    public Task<string?> GetStudentCodeByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return Context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.StudentCode)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> AddStudentsToClassAsync(Guid classId, IEnumerable<string> studentCodes, CancellationToken ct = default)
    {
        int count = 0;
        foreach (var rawCode in studentCodes)
        {
            var code = rawCode.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) continue;

            // 1. Ensure User exists
            var user = await Context.Users.FirstOrDefaultAsync(u => u.StudentCode == code, ct);
            if (user == null)
            {
                var defaultEmail = $"{code.ToLowerInvariant()}@fpt.edu.vn";
                user = await Context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == defaultEmail.ToLower(), ct);
                if (user == null)
                {
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        StudentCode = code,
                        Email = defaultEmail,
                        FullName = code,
                        Role = UserRole.Student,
                        CreatedAt = DateTime.UtcNow
                    };
                    Context.Users.Add(user);
                    await Context.SaveChangesAsync(ct);
                }
                else if (string.IsNullOrEmpty(user.StudentCode))
                {
                    user.StudentCode = code;
                    await Context.SaveChangesAsync(ct);
                }
            }

            // 2. Ensure StudentProfile exists
            var profile = await Context.StudentProfiles.FirstOrDefaultAsync(p => p.StudentCode == code, ct);
            if (profile == null)
            {
                profile = new StudentProfile
                {
                    StudentCode = code,
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow
                };
                Context.StudentProfiles.Add(profile);
                await Context.SaveChangesAsync(ct);
            }

            // 3. Ensure StudentClass exists
            var existsInClass = await Context.StudentClasses
                .AnyAsync(sc => sc.ClassId == classId && sc.StudentCode == code, ct);

            if (!existsInClass)
            {
                Context.StudentClasses.Add(new StudentClass
                {
                    ClassId = classId,
                    StudentCode = code,
                    Status = 1
                });
                count++;
            }
        }

        if (count > 0)
        {
            await Context.SaveChangesAsync(ct);
        }

        return count;
    }

    public async Task<bool> RemoveStudentFromClassAsync(Guid classId, string studentCode, CancellationToken ct = default)
    {
        var code = studentCode.Trim().ToUpperInvariant();
        var sc = await Context.StudentClasses
            .FirstOrDefaultAsync(x => x.ClassId == classId && x.StudentCode == code, ct);

        if (sc == null) return false;

        Context.StudentClasses.Remove(sc);
        await Context.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct = default)
    {
        return DbSet.AnyAsync(c => c.Id == classId, ct);
    }
}

