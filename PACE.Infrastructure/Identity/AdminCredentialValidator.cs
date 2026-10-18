using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using PACE.Application.Common.Interfaces;
namespace PACE.Infrastructure.Identity;
// AdminLogin:PasswordHash = base64(16+ byte salt):base64(PBKDF2-SHA256(password,salt,210000,32)).
// Missing or invalid configuration fails closed. Secrets never appear in errors or logs.
public sealed class AdminCredentialValidator(IConfiguration config) : IAdminCredentialValidator
{
    public bool Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return false;
        try
        {
            var parts = (config["AdminLogin:PasswordHash"] ?? "").Split(':'); if (parts.Length != 2) return false;
            var salt = Convert.FromBase64String(parts[0]); var expected = Convert.FromBase64String(parts[1]); if (salt.Length < 16 || expected.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32); return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException) { return false; }
    }
}
