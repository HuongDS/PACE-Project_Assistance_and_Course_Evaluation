using PACE.Application.Dtos;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<GoogleStudentLoginResponse> GoogleStudentLoginAsync(GoogleStudentLoginRequest request, CancellationToken ct = default);
    Task<LoginResponse> GoogleStaffLoginAsync(GoogleStudentLoginRequest request, CancellationToken ct = default);
    Task<UserDto> GetMeAsync(CancellationToken ct = default);
    Task SendOtpAsync(SendOtpRequest request, CancellationToken ct = default);
    Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
}
