using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await authService.LoginAsync(request, ct));

    [HttpPost("google/student")]
    [AllowAnonymous]
    public async Task<ActionResult<GoogleStudentLoginResponse>> GoogleStudentLogin(GoogleStudentLoginRequest request, CancellationToken ct)
        => Ok(await authService.GoogleStudentLoginAsync(request, ct));

    [HttpPost("google/staff")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> GoogleStaffLogin(GoogleStudentLoginRequest request, CancellationToken ct)
        => Ok(await authService.GoogleStaffLoginAsync(request, ct));

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
        => Ok(await authService.GetMeAsync(ct));
}
