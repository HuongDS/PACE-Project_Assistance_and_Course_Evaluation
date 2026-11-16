using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.API.Common;

public static class AdminSeeder
{
    public static async Task SeedAdminsAsync(this WebApplication app)
    {
        var emails = app.Configuration.GetSection("Auth:SeedAdminEmails").Get<string[]>() ?? [];
        var emailList = emails.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim().ToLowerInvariant()).Distinct().ToList();
        if (!emailList.Contains("manager")) emailList.Add("manager");
        if (!emailList.Contains("admin@pace.edu.vn")) emailList.Add("admin@pace.edu.vn");

        using var scope = app.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();

        try
        {
            var defaultHash = BCrypt.Net.BCrypt.HashPassword("PacePassword#2026");

            foreach (var email in emailList)
            {
                var user = await uow.Users.GetByEmailAsync(email);
                if (user == null)
                {
                    await uow.Users.AddAsync(new User
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        FullName = email == "admin@pace.edu.vn" ? "Quản trị viên Hệ thống" : "Hệ thống Quản trị",
                        Role = UserRole.Admin,
                        PasswordHash = defaultHash,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(user.PasswordHash))
                    {
                        user.PasswordHash = defaultHash;
                    }
                    if (user.Role != UserRole.Admin)
                    {
                        user.Role = UserRole.Admin;
                    }
                    if (string.IsNullOrWhiteSpace(user.FullName) && email == "admin@pace.edu.vn")
                    {
                        user.FullName = "Quản trị viên Hệ thống";
                    }
                }
            }

            await uow.SaveChangesAsync();
            logger.LogInformation("Đã đảm bảo tài khoản Admin ({Count} accounts) với thông tin cấu hình", emailList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không seed được admin từ cấu hình");
        }
    }
}
