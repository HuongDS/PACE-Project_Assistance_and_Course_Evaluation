using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PACE.Infrastructure.Persistence;

namespace PACE.API.HealthChecks;

/// <summary>Kiểm tra kết nối PostgreSQL. Đây là phụ thuộc bắt buộc nên lỗi được tính là Unhealthy.</summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Kết nối PostgreSQL thành công.")
                : HealthCheckResult.Unhealthy("Không kết nối được PostgreSQL.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Kiểm tra PostgreSQL quá thời gian chờ.");
        }
        catch (Exception ex)
        {
            // Chi tiết lỗi chỉ nằm trong log của server, không đưa ra response.
            return HealthCheckResult.Unhealthy("Không kết nối được PostgreSQL.", ex);
        }
    }
}
