using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PACE.Application.Common.Interfaces;

namespace PACE.API.HealthChecks;

/// <summary>
/// Kiểm tra Redis (Upstash) bằng một lệnh GET đọc-only qua chính IOtpCacheService mà luồng OTP đang dùng,
/// nên phản ánh đúng đường đi thật. Redis chỉ ảnh hưởng đăng nhập OTP nên lỗi chỉ là Degraded.
/// </summary>
public sealed class RedisHealthCheck(IServiceProvider services, IConfiguration configuration) : IHealthCheck
{
    internal static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(3);
    private const string ProbeKey = "pace-health-probe@invalid.local";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuration["Redis:ConnectionString"]))
            return HealthCheckResult.Degraded("Redis:ConnectionString chưa được cấu hình.");

        try
        {
            // Resolve và gọi trong Task.Run: lần đầu service mở kết nối đồng bộ, có thể chặn vài giây.
            await Task.Run(async () =>
            {
                var cache = services.GetRequiredService<IOtpCacheService>();
                await cache.GetOtpAsync(ProbeKey, cancellationToken);
            }, cancellationToken).WaitAsync(PingTimeout, cancellationToken);
            return HealthCheckResult.Healthy("Redis phản hồi.");
        }
        catch (TimeoutException)
        {
            return HealthCheckResult.Degraded("Redis không phản hồi trong thời gian cho phép.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded("Kiểm tra Redis quá thời gian chờ.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Không kết nối được Redis.", ex);
        }
    }
}
