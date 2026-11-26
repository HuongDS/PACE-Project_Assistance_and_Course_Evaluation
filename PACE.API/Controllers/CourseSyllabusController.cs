using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.API.Common;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

/// <summary>Một dòng trong bảng điểm của syllabus. <c>Part</c> là số lần kiểm tra của cột, <c>Weight</c> là tổng của cả cột.</summary>
public record SyllabusAssessment(
    [Required, MaxLength(255)] string Category,
    [MaxLength(64)] string? Type,
    [Range(1, 50)] int Part,
    [Range(0.0, 100.0)] decimal Weight,
    [MaxLength(8000)] string? GradingGuide,
    [MaxLength(8000)] string? Note,
    [MaxLength(30)] List<string>? Clos);

/// <summary>Một tiết trong lịch dạy của syllabus.</summary>
public record SyllabusSession(
    [Range(1, 500)] int Number,
    [Required, MaxLength(1000)] string Topic);

public record SaveCourseSyllabus(
    [MaxLength(64)] string? SourceId,
    [Required, MaxLength(255)] string Name,
    [Required, MinLength(1), MaxLength(50)] List<SyllabusAssessment> Assessments,
    [Required, MinLength(1), MaxLength(500)] List<SyllabusSession> Sessions);

public record CourseSyllabusView(
    Guid CourseId, string? SourceId, string Name, DateTime UpdatedAt,
    List<SyllabusAssessment> Assessments, List<SyllabusSession> Sessions);

public static class SyllabusRules
{
    private record Content(List<SyllabusAssessment> Assessments, List<SyllabusSession> Sessions);

    /// <summary>Kiểm tra các ràng buộc giữa các dòng rồi trả nội dung để lưu, tiết học xếp theo thứ tự.</summary>
    public static string Serialize(SaveCourseSyllabus request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Syllabus name is required.");
        if (request.Assessments.Any(x => x == null || string.IsNullOrWhiteSpace(x.Category)))
            throw new BadRequestException("Every assessment needs a category.");
        // Cho phép lệch nhỏ do làm tròn ở nguồn.
        if (request.Assessments.Sum(x => x.Weight) > 100.5m)
            throw new BadRequestException("Assessment weights cannot add up to more than 100.");
        if (request.Sessions.Any(x => x == null || string.IsNullOrWhiteSpace(x.Topic)))
            throw new BadRequestException("Every session needs a topic.");
        if (request.Sessions.Select(x => x.Number).Distinct().Count() != request.Sessions.Count)
            throw new BadRequestException("Session numbers must be unique.");

        return JsonSerializer.Serialize(new Content(request.Assessments, [.. request.Sessions.OrderBy(x => x.Number)]));
    }

    public static CourseSyllabusView View(CourseSyllabus item)
    {
        var content = JsonSerializer.Deserialize<Content>(item.ContentJson)!;
        return new(item.CourseId, item.SourceId, item.Name, item.UpdatedAt, content.Assessments, content.Sessions);
    }
}

[ApiController, Authorize, AllowTrainingOffice, Route("api/courses/{courseId:guid}/syllabus")]
public class CourseSyllabusController(AppDbContext db) : ControllerBase
{
    /// <summary>Syllabus của môn. Trả 204 khi môn chưa có.</summary>
    [HttpGet, Authorize(Roles = Roles.ClassStaff)]
    public async Task<ActionResult<CourseSyllabusView>> Get(Guid courseId, CancellationToken ct)
    {
        var item = await db.CourseSyllabi.AsNoTracking().SingleOrDefaultAsync(x => x.CourseId == courseId, ct);
        return item == null ? NoContent() : Ok(SyllabusRules.View(item));
    }

    /// <summary>Nhập hoặc thay syllabus của môn (Phòng đào tạo / Admin).</summary>
    [HttpPut, Authorize(Roles = Roles.Registrar)]
    public async Task<ActionResult<CourseSyllabusView>> Save(Guid courseId, SaveCourseSyllabus request, CancellationToken ct)
    {
        var content = SyllabusRules.Serialize(request);
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, ct))
            throw new NotFoundException("Course unavailable.");

        var item = await db.CourseSyllabi.SingleOrDefaultAsync(x => x.CourseId == courseId, ct);
        if (item == null)
            db.CourseSyllabi.Add(item = new CourseSyllabus { CourseId = courseId });
        item.SourceId = request.SourceId?.Trim();
        item.Name = request.Name.Trim();
        item.ContentJson = content;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(SyllabusRules.View(item));
    }
}
