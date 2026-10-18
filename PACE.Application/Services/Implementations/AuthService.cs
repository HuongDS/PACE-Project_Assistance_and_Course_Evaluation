using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.Services.Interfaces;

namespace PACE.Application.Services.Implementations;

public class AuthService(IUnitOfWork uow, IJwtTokenService jwt, ICurrentUserService currentUser, IGoogleIdTokenValidator google, IAdminCredentialValidator? adminCredentials = null) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await uow.Users.GetByEmailAsync(request.Email, ct)
                   ?? throw new UnauthorizedException("Email chưa được cấp quyền truy cập hệ thống.");

        if (user.Role != PACE.Domain.Enums.UserRole.Admin)
            throw new UnauthorizedException("Sinh viên phải đăng nhập bằng Google.");

        if (adminCredentials?.Validate(request.Password) != true)
            throw new UnauthorizedException("Invalid administrator credentials.");
        var (token, expiresAt) = jwt.CreateToken(user);
        return new LoginResponse(token, expiresAt, user.ToDto());
    }

    public async Task<GoogleStudentLoginResponse> GoogleStudentLoginAsync(GoogleStudentLoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            throw new UnauthorizedException("Google ID Token không hợp lệ.");

        var email = await google.ValidateEmailAsync(request.IdToken, ct);
        var user = await uow.Users.GetByEmailAsync(email, ct);
        if (user is null || user.Role != PACE.Domain.Enums.UserRole.Student)
            throw new UnauthorizedException("Email chưa được cấp quyền sinh viên.");

        var (token, expiresAt) = jwt.CreateToken(user);
        return new GoogleStudentLoginResponse(token, expiresAt,
            new StudentLoginInfo(user.Id, user.StudentCode, user.Email));
    }

    public async Task<LoginResponse> GoogleStaffLoginAsync(GoogleStudentLoginRequest request, CancellationToken ct = default)
    {
        var email = await google.ValidateEmailAsync(request.IdToken, ct);
        var user = await uow.Users.GetByEmailAsync(email, ct);
        if (user is null || user.Role is not (PACE.Domain.Enums.UserRole.Admin or PACE.Domain.Enums.UserRole.Lecturer))
            throw new UnauthorizedException("Registered staff account required.");
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
