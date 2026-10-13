using PACE.BLL.Dtos;
using PACE.DAL.Models;

namespace PACE.BLL.Services.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto> GetMeAsync(CancellationToken ct = default);
}
