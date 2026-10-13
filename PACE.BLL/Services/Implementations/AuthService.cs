using PACE.BLL.Common;
using PACE.BLL.Dtos;
using PACE.BLL.Exceptions;
using PACE.BLL.Mappings;
using PACE.BLL.Services.Interfaces;
using PACE.DAL.Repositories.Interfaces;

namespace PACE.BLL.Services.Implementations;

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
