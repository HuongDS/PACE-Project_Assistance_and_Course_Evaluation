using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.API.Common;
using PACE.Application.Common.Exceptions;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record SyllabusVersionSummary(Guid Id, Guid SemesterId, string SemesterName, string Name, string? SourceId, DateTime UpdatedAt, int Assessments, int Sessions);
public record CatalogCourse(Guid Id, string Code, string Name, int Credits, int ClassCount, int TopicCount, List<SyllabusVersionSummary> Syllabi);
public record UpdateCourseRequest([Required, MaxLength(40)] string Code, [Required, MaxLength(255)] string Name, [Range(1, 20)] int Credits);

public static class CourseRules
{
    /// <summary>Môn đã dùng thì không xóa, để không kéo theo lớp, đề tài hay bài đã làm.</summary>
    public static void RequireUnused(int classes, int topics, int projects)
    {
        if (classes > 0 || topics > 0 || projects > 0)
            throw new ConflictException($"Môn đang có {classes} lớp, {topics} đề tài mẫu và {projects} dự án nên không xóa được.");
    }
}

[ApiController, Authorize, AllowTrainingOffice, Route("api/courses")]
public class CourseCatalogController(AppDbContext db) : ControllerBase
{
    /// <summary>Danh mục môn gọn nhẹ: số lớp, số đề tài và các phiên bản syllabus.</summary>
    [HttpGet("catalog"), Authorize(Roles = Roles.ClassStaff)]
    public async Task<ActionResult<List<CatalogCourse>>> Catalog(CancellationToken ct)
    {
        var courses = await db.Courses.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new { x.Id, x.Code, x.Name, x.Credits, Classes = x.Classes.Count, Topics = x.Topics.Count }).ToListAsync(ct);
        var versions = await db.CourseSyllabi.AsNoTracking()
            .OrderByDescending(x => x.Semester.StartDate)
            .Select(x => new { x.Id, x.CourseId, x.SemesterId, Semester = x.Semester.Name, x.Name, x.SourceId, x.UpdatedAt, x.ContentJson })
            .ToListAsync(ct);
        var byCourse = versions.ToLookup(x => x.CourseId);
        return Ok(courses.Select(c => new CatalogCourse(c.Id, c.Code, c.Name, c.Credits, c.Classes, c.Topics,
            byCourse[c.Id].Select(v =>
            {
                var count = SyllabusRules.Count(v.ContentJson);
                return new SyllabusVersionSummary(v.Id, v.SemesterId, v.Semester, v.Name, v.SourceId, v.UpdatedAt, count.Assessments, count.Sessions);
            }).ToList())).ToList());
    }

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Registrar)]
    public async Task<IActionResult> Update(Guid id, UpdateCourseRequest request, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Course unavailable.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (code.Length == 0 || string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Mã môn và tên môn không được để trống.");
        if (await db.Courses.AnyAsync(x => x.Id != id && x.Code.ToUpper() == code, ct))
            throw new ConflictException($"Môn học với mã '{code}' đã tồn tại.");

        course.Code = code;
        course.Name = request.Name.Trim();
        course.Credits = request.Credits;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Registrar)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Course unavailable.");
        CourseRules.RequireUnused(
            await db.Classes.CountAsync(x => x.CourseId == id, ct),
            await db.CourseTopics.CountAsync(x => x.CourseId == id, ct),
            await db.CourseProjects.CountAsync(x => x.CourseId == id, ct));
        db.Courses.Remove(course);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
