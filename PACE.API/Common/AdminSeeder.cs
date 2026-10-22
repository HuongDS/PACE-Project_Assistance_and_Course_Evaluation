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

        using var scope = app.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();

        try
        {
            var existing = await uow.Users.GetAllEmailsAsync();
            var missing = emailList.Except(existing).ToList();
            if (missing.Count == 0) return;

            await uow.Users.AddRangeAsync(missing.Select(e => new User { Id = Guid.NewGuid(), Email = e, Role = UserRole.Admin }));
            await uow.SaveChangesAsync();
            logger.LogInformation("Đã tạo {Count} tài khoản admin/manager từ cấu hình", missing.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không seed được admin từ cấu hình");
        }
    }
}
