using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.BLL.Dtos;
using PACE.BLL.Exceptions;
using PACE.BLL.Services.Interfaces;
using PACE.DAL.Repositories;

namespace PACE.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> GetAll([FromQuery] UserQuery query, CancellationToken ct)
        => Ok(await userService.GetPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await userService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
        => Ok(await userService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await userService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportUsersResult>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new BadRequestException("Chưa chọn file Excel.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ hỗ trợ file .xlsx.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;
        return Ok(await userService.ImportFromExcelAsync(stream, ct));
    }
}
