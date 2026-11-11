namespace PACE.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken ct = default);
    Task SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken ct = default);
}

