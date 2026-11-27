using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

/// <summary>
/// CHỈ DÙNG KHI DEV. Controller này bị gỡ khỏi app (route trả 404) trừ khi
/// Auth:DevLoginEnabled = true và môi trường là Development (xem Program.cs).
/// </summary>
[ApiController]
[Route("api/auth")]
public class DevAuthController(IDevLoginService devLogin) : ControllerBase
{
    [HttpPost("dev-login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> DevLogin(DevLoginRequest request, CancellationToken ct)
        => Ok(await devLogin.LoginAsync(request, ct));
}
