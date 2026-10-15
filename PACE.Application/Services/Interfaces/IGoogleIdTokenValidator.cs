namespace PACE.Application.Services.Interfaces;

public interface IGoogleIdTokenValidator
{
    Task<string> ValidateEmailAsync(string idToken, CancellationToken ct = default);
}
