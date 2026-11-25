using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.API.Common;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record LecturerOption(Guid Id, string FullName, string Email);

[ApiController, Route("api/lecturers"), Authorize(Roles = Roles.Registrar), AllowTrainingOffice]
public class LecturersController(AppDbContext db) : ControllerBase
{
    /// <summary>Giảng viên để phòng đào tạo phân phụ trách lớp.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LecturerOption>>> List(CancellationToken ct)
        => Ok(await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Lecturer)
            .OrderBy(x => x.FullName).ThenBy(x => x.Email)
            .Select(x => new LecturerOption(x.Id, x.FullName, x.Email))
            .ToListAsync(ct));
}
