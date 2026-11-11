using System.ComponentModel.DataAnnotations;

namespace PACE.Application.Dtos;

public class LoginRequest
{
    [Required, MaxLength(1024)]
    public string Password { get; set; } = "";

    [Required, MaxLength(255)]
    public string Email { get; set; } = null!;
}

public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User, string? RefreshToken = null);

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}

public class GoogleStudentLoginRequest
{
    [Required, MaxLength(16384)]
    public string IdToken { get; set; } = null!;
}

public class SendOtpRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = null!;
}

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required, MinLength(6), MaxLength(100)]
    public string Password { get; set; } = null!;

    [Required, MaxLength(255)]
    public string FullName { get; set; } = null!;

    [MaxLength(20)]
    public string? StudentCode { get; set; }

    [Required, Length(6, 6)]
    public string Otp { get; set; } = null!;
}

public record StudentLoginInfo(Guid UserId, string? StudentId, string Email);
public record GoogleStudentLoginResponse(string AccessToken, DateTime ExpiresAt, StudentLoginInfo Student);
