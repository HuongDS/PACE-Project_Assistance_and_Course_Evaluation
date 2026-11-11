using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using PACE.Application.Common.Interfaces;

namespace PACE.Infrastructure.Services;

public class MailKitEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<MailKitEmailService> _logger;

    public MailKitEmailService(IConfiguration config, ILogger<MailKitEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken ct = default)
    {
        var smtpHost = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
        var smtpPort = int.TryParse(_config["Email:SmtpPort"], out var port) ? port : 587;
        var smtpUser = _config["Email:SmtpUser"];
        var smtpPass = _config["Email:SmtpPassword"];
        var senderName = _config["Email:SenderName"] ?? "PACE System";
        var senderEmail = _config["Email:SenderEmail"] ?? (smtpUser ?? "no-reply@pace.edu.vn");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder();
        if (isHtml)
        {
            bodyBuilder.HtmlBody = body;
        }
        else
        {
            bodyBuilder.TextBody = body;
        }

        message.Body = bodyBuilder.ToMessageBody();

        if (string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
        {
            _logger.LogWarning("Email configuration (SmtpUser / SmtpPassword) not provided. Email not sent via SMTP. Content:\nTo: {ToEmail}\nSubject: {Subject}\nBody: {Body}",
                toEmail, subject, body);
            return;
        }

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(smtpUser, smtpPass, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
            throw;
        }
    }

    public async Task SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken ct = default)
    {
        var subject = "[PACE] Mã xác nhận đăng ký tài khoản";
        var body = $"""
        <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e0e0e0; border-radius: 12px; background-color: #fafafa;">
            <h2 style="color: #2dd4a8; margin-top: 0;">PACE - Xác thực tài khoản</h2>
            <p>Chào bạn,</p>
            <p>Bạn đang đăng ký tài khoản trên hệ thống <strong>PACE</strong> (Project Assistance and Course Evaluation).</p>
            <p>Mã OTP xác thực của bạn là:</p>
            <div style="background-color: #141b18; color: #2dd4a8; font-size: 32px; font-weight: bold; letter-spacing: 6px; padding: 16px; text-align: center; border-radius: 8px; margin: 24px 0;">
                {otpCode}
            </div>
            <p style="color: #666; font-size: 14px;">Mã này có hiệu lực trong vòng <strong>5 phút</strong>. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
            <hr style="border: none; border-top: 1px solid #e0e0e0; margin: 24px 0;" />
            <p style="color: #999; font-size: 12px; text-align: center;">FPT University HCM · PACE Project</p>
        </div>
        """;

        await SendEmailAsync(toEmail, subject, body, isHtml: true, ct);
    }
}
