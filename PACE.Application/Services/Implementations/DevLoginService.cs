using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.Services.Interfaces;

namespace PACE.Application.Services.Implementations;

/// <summary>
/// Dev Login: tìm user có sẵn theo email rồi phát JWT bằng đúng IJwtTokenService của luồng login thật
/// (cùng key, issuer, audience, claims, thời hạn). Role lấy từ DB. Không tạo user mới.
/// </summary>
public sealed class DevLoginService(IUnitOfWork uow, IJwtTokenService jwt) : IDevLoginService
{
    public async Task<LoginResponse> LoginAsync(DevLoginRequest request, CancellationToken ct = default)
    {
        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0)
            throw new BadRequestException("Vui lòng nhập email.");

        var user = await uow.Users.GetByEmailAsync(email, ct)
                   ?? throw new UnauthorizedException("Email không tồn tại trong hệ thống.");

        var (token, expiresAt) = jwt.CreateToken(user);
        return new LoginResponse(token, expiresAt, user.ToDto());
    }
}
