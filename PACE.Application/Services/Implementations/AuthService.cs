using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.Services.Interfaces;

namespace PACE.Application.Services.Implementations;

public class AuthService(
    IUnitOfWork uow,
    IJwtTokenService jwt,
    ICurrentUserService currentUser,
    IGoogleIdTokenValidator google,
    IPasswordHasher passwordHasher,
    IOtpCacheService otpCache,
    IEmailService emailService,
    IAdminCredentialValidator? adminCredentials = null) : IAuthService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await uow.Users.GetByEmailAsync(normalizedEmail, ct)
                   ?? await uow.Users.GetByEmailAsync(request.Email.Trim(), ct)
                   ?? throw new UnauthorizedException("Tài khoản hoặc mật khẩu không chính xác.");

        // Kiểm tra mật khẩu mã hóa BCrypt
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                throw new UnauthorizedException("Tài khoản hoặc mật khẩu không chính xác.");

            user.RefreshToken = GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await uow.SaveChangesAsync(ct);

            var (token, expiresAt) = jwt.CreateToken(user);
            return new LoginResponse(token, expiresAt, user.ToDto(), user.RefreshToken);
        }

        // Với tài khoản Admin cấu hình qua hash appsettings
        if (user.Role == PACE.Domain.Enums.UserRole.Admin && adminCredentials?.Validate(request.Password) == true)
        {
            user.RefreshToken = GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await uow.SaveChangesAsync(ct);

            var (token, expiresAt) = jwt.CreateToken(user);
            return new LoginResponse(token, expiresAt, user.ToDto(), user.RefreshToken);
        }

        throw new UnauthorizedException("Tài khoản chưa được thiết lập mật khẩu hoặc phải đăng nhập bằng Google.");
    }

    public async Task SendOtpAsync(SendOtpRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await uow.Users.EmailExistsAsync(email, null, ct))
        {
            throw new ConflictException("Email này đã được đăng ký trong hệ thống.");
        }

        // Sinh mã OTP 6 số
        var otpCode = Random.Shared.Next(100000, 999999).ToString();
        await otpCache.SetOtpAsync(email, otpCode, TimeSpan.FromMinutes(5), ct);

        // Gửi qua MailKit
        await emailService.SendOtpEmailAsync(email, otpCode, ct);
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var cachedOtp = await otpCache.GetOtpAsync(email, ct);

        if (string.IsNullOrWhiteSpace(cachedOtp) || cachedOtp != request.Otp.Trim())
        {
            throw new BadRequestException("Mã OTP không chính xác hoặc đã hết hạn.");
        }

        if (await uow.Users.EmailExistsAsync(email, null, ct))
        {
            throw new ConflictException("Email này đã được đăng ký.");
        }

        // Mã hóa mật khẩu bằng BCrypt
        var hashedPassword = passwordHasher.HashPassword(request.Password);

        var refreshToken = GenerateRefreshToken();
        var newUser = new PACE.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = request.FullName.Trim(),
            StudentCode = request.StudentCode?.Trim().ToUpperInvariant(),
            Role = PACE.Domain.Enums.UserRole.Student, // Mặc định luôn là sinh viên
            PasswordHash = hashedPassword,
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        await uow.Users.AddAsync(newUser, ct);
        await uow.SaveChangesAsync(ct);

        // Xóa OTP sau khi đăng ký thành công
        await otpCache.RemoveOtpAsync(email, ct);

        var (token, expiresAt) = jwt.CreateToken(newUser);
        return new LoginResponse(token, expiresAt, newUser.ToDto(), refreshToken);
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new BadRequestException("Refresh token không hợp lệ.");

        var user = await uow.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken, asNoTracking: false, ct);
        if (user is null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Refresh token đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại.");
        }

        // Tạo access token mới & xoay vòng refresh token
        var newRefreshToken = GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await uow.SaveChangesAsync(ct);

        var (newToken, newExpiresAt) = jwt.CreateToken(user);
        return new LoginResponse(newToken, newExpiresAt, user.ToDto(), newRefreshToken);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
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
