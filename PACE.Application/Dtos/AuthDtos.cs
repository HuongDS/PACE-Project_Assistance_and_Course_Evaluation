using System.ComponentModel.DataAnnotations;

namespace PACE.Application.Dtos;

public class LoginRequest
{
    [Required, MaxLength(1024)]
    public string Password { get; set; } = "";

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = null!;
}

public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

// Temporary form entry while student Google sign-in is disabled.
public class StudentQuickLoginRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = null!;

    [Required, MaxLength(50)]
    public string StudentCode { get; set; } = null!;
}

public class GoogleStudentLoginRequest
{
    [Required, MaxLength(16384)]
    public string IdToken { get; set; } = null!;
}

public record StudentLoginInfo(Guid UserId, string? StudentId, string Email);
public record GoogleStudentLoginResponse(string AccessToken, DateTime ExpiresAt, StudentLoginInfo Student);
