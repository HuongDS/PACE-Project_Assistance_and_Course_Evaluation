using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using PACE.Application.Common.Exceptions;
using PACE.Application.Services.Interfaces;

namespace PACE.Infrastructure.Identity;

public sealed class GoogleIdTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleIdTokenValidator
{
    public async Task<string> ValidateEmailAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ClientId))
            throw new InvalidOperationException("GoogleAuth:ClientId phải được cấu hình.");
        if (string.IsNullOrWhiteSpace(idToken))
            throw new UnauthorizedException("Google ID Token không hợp lệ.");

        ct.ThrowIfCancellationRequested();
        try
        {
            // Google validates signature against its cached public keys, issuer, audience and lifetime.
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [options.Value.ClientId],
                    ExpirationTimeClockTolerance = TimeSpan.FromMinutes(5)
                }).WaitAsync(ct);
            if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
                throw new UnauthorizedException("Email Google chưa được xác minh.");
            return payload.Email.Trim().ToLowerInvariant();
        }
        catch (InvalidJwtException)
        {
            throw new UnauthorizedException("Google ID Token không hợp lệ hoặc đã hết hạn.");
        }
    }
}
