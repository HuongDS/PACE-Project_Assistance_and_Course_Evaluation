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
    [Required] Guid? SemesterId,
    [MaxLength(64)] string? SourceId,
    [Required, MaxLength(255)] string Name,
    [Required, MinLength(1), MaxLength(50)] List<SyllabusAssessment> Assessments,
    [Required, MinLength(1), MaxLength(500)] List<SyllabusSession> Sessions);

public record CourseSyllabusView(
    Guid Id, Guid CourseId, Guid SemesterId, string SemesterName, string? SourceId, string Name, DateTime UpdatedAt,
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
        return new(item.Id, item.CourseId, item.SemesterId, item.Semester?.Name ?? "", item.SourceId, item.Name, item.UpdatedAt, content.Assessments, content.Sessions);
    }

    /// <summary>Số cột điểm và số tiết, để liệt kê phiên bản mà không gửi cả nội dung.</summary>
    public static (int Assessments, int Sessions) Count(string contentJson)
    {
        var content = JsonSerializer.Deserialize<Content>(contentJson)!;
        return (content.Assessments.Count, content.Sessions.Count);
    }

    /// <summary>Phiên bản áp dụng cho một kỳ: bản có kỳ bắt đầu muộn nhất nhưng không sau kỳ đó. Không chỉ rõ kỳ thì lấy bản mới nhất.</summary>
    public static CourseSyllabus? Effective(IEnumerable<CourseSyllabus> versions, DateOnly? semesterStart) =>
        versions.Where(x => semesterStart == null || x.Semester.StartDate <= semesterStart)
            .OrderByDescending(x => x.Semester.StartDate).ThenByDescending(x => x.UpdatedAt).FirstOrDefault();
}

[ApiController, Authorize, AllowTrainingOffice, Route("api/courses/{courseId:guid}/syllabus")]
public class CourseSyllabusController(AppDbContext db) : ControllerBase
{
    /// <summary>Syllabus áp dụng cho kỳ của một lớp (classId) hoặc một kỳ (semesterId); không truyền thì lấy bản mới nhất.
    /// Trả 204 khi môn chưa có bản nào phù hợp.</summary>
    [HttpGet, Authorize(Roles = Roles.ClassStaff)]
    public async Task<ActionResult<CourseSyllabusView>> Get(Guid courseId, [FromQuery] Guid? classId, [FromQuery] Guid? semesterId, CancellationToken ct)
    {
        DateOnly? start = null;
        if (classId != null)
        {
            var target = await db.Classes.AsNoTracking().Where(x => x.Id == classId && x.CourseId == courseId)
                .Select(x => new { Start = x.Semester != null ? x.Semester.StartDate : x.StartDate }).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Class unavailable.");
            start = target.Start;
        }
        else if (semesterId != null)
            start = await db.Semesters.AsNoTracking().Where(x => x.Id == semesterId).Select(x => (DateOnly?)x.StartDate).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Semester unavailable.");

        var versions = await db.CourseSyllabi.AsNoTracking().Include(x => x.Semester).Where(x => x.CourseId == courseId).ToListAsync(ct);
        var item = SyllabusRules.Effective(versions, start);
        return item == null ? NoContent() : Ok(SyllabusRules.View(item));
    }

    [HttpGet("{id:guid}"), Authorize(Roles = Roles.ClassStaff)]
    public async Task<ActionResult<CourseSyllabusView>> GetVersion(Guid courseId, Guid id, CancellationToken ct) =>
        Ok(SyllabusRules.View(await Find(courseId, id, ct)));

    /// <summary>Nhập syllabus áp dụng từ một kỳ; môn đã có bản của kỳ đó thì thay bản ấy (Phòng đào tạo / Admin).</summary>
    [HttpPut, Authorize(Roles = Roles.Registrar)]
    public async Task<ActionResult<CourseSyllabusView>> Save(Guid courseId, SaveCourseSyllabus request, CancellationToken ct)
    {
        var content = SyllabusRules.Serialize(request);
        if (!await db.Courses.AnyAsync(x => x.Id == courseId, ct))
            throw new NotFoundException("Course unavailable.");
        var semester = await db.Semesters.SingleOrDefaultAsync(x => x.Id == request.SemesterId, ct)
            ?? throw new NotFoundException("Semester unavailable.");

        var item = await db.CourseSyllabi.SingleOrDefaultAsync(x => x.CourseId == courseId && x.SemesterId == semester.Id, ct);
        if (item == null)
            db.CourseSyllabi.Add(item = new CourseSyllabus { CourseId = courseId, SemesterId = semester.Id });
        item.Semester = semester;
        item.SourceId = request.SourceId?.Trim();
        item.Name = request.Name.Trim();
        item.ContentJson = content;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(SyllabusRules.View(item));
    }

    /// <summary>Gỡ một phiên bản. Mốc đã giao cho lớp không đổi vì được chép riêng vào lớp.</summary>
    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Registrar)]
    public async Task<IActionResult> Delete(Guid courseId, Guid id, CancellationToken ct)
    {
        db.CourseSyllabi.Remove(await Find(courseId, id, ct));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<CourseSyllabus> Find(Guid courseId, Guid id, CancellationToken ct) =>
        await db.CourseSyllabi.Include(x => x.Semester).SingleOrDefaultAsync(x => x.CourseId == courseId && x.Id == id, ct)
        ?? throw new NotFoundException("Syllabus unavailable.");
}
