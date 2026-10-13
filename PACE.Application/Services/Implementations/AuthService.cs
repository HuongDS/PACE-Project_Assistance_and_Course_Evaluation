using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.Services.Interfaces;

namespace PACE.Application.Services.Implementations;

public class AuthService(IUnitOfWork uow, IJwtTokenService jwt, ICurrentUserService currentUser) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await uow.Users.GetByEmailAsync(request.Email, ct)
                   ?? throw new UnauthorizedException("Email chưa được cấp quyền truy cập hệ thống.");

        var (token, expiresAt) = jwt.CreateToken(user);
        return new LoginResponse(token, expiresAt, user.ToDto());
    }

    public async Task<UserDto> GetMeAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated) throw new UnauthorizedException("Bạn chưa đăng nhập.");

        var user = await uow.Users.GetWithRepoAsync(currentUser.UserId, ct: ct)
                   ?? throw new UnauthorizedException("Tài khoản không còn tồn tại.");
        return user.ToDto();
    }
}
