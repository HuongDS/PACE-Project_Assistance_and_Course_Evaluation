using System.ComponentModel.DataAnnotations;

namespace PACE.BLL.Dtos;

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = null!;
}

public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User);
