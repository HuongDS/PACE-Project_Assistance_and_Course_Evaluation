using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PACE.API.HealthChecks;

public static class HealthCheckExtensions
{
    /// <summary>Tag của các check phụ thuộc bên ngoài, chỉ chạy ở /health/details.</summary>
    public const string DependencyTag = "dependency";

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10);

    // Giữ nguyên dấu tiếng Việt thay vì escape thành \uXXXX.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Đăng ký các health check thật. /health vẫn chỉ là liveness (không chạy check nào) để Azure và workflow deploy
    /// không bị rollback chỉ vì Redis hay AI lỗi; /health/details chạy toàn bộ check dưới đây.
    /// </summary>
    public static IServiceCollection AddPaceHealthChecks(this IServiceCollection services)
    {
        var tags = new[] { DependencyTag };
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("postgresql", HealthStatus.Unhealthy, tags, CheckTimeout)
            .AddCheck<RedisHealthCheck>("redis", HealthStatus.Degraded, tags, CheckTimeout)
            .AddCheck<GitCliHealthCheck>("git", HealthStatus.Degraded, tags, CheckTimeout)
            .AddCheck<AiProvidersHealthCheck>("ai-providers", HealthStatus.Degraded, tags, CheckTimeout);
        return services;
    }

    /// <summary>
    /// Ghi báo cáo dạng JSON cho Admin. Chỉ in tên, trạng thái, mô tả, thời gian và Data của từng check;
    /// KHÔNG in exception (có thể chứa host hoặc chuỗi kết nối).
    /// </summary>
    public static Task WriteDetailedReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = (long)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = (long)e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data.ToDictionary(d => d.Key, d => d.Value?.ToString())
            })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
