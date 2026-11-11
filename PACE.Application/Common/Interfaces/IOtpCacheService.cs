namespace PACE.Application.Common.Interfaces;

public interface IOtpCacheService
{
    Task SetOtpAsync(string email, string otpCode, TimeSpan expiry, CancellationToken ct = default);
    Task<string?> GetOtpAsync(string email, CancellationToken ct = default);
    Task RemoveOtpAsync(string email, CancellationToken ct = default);
}

