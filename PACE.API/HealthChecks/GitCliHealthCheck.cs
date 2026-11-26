using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PACE.Application.Analysis;

namespace PACE.API.HealthChecks;

/// <summary>Kiểm tra git chạy được (<c>git --version</c>). Thiếu git chỉ làm hỏng phân tích repo nên là Degraded.</summary>
public sealed class GitCliHealthCheck(IOptions<AnalysisOptions> options) : IHealthCheck
{
    internal static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var executable = string.IsNullOrWhiteSpace(options.Value.GitExecutable) ? "git" : options.Value.GitExecutable;
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--version");

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null) return HealthCheckResult.Degraded("Không chạy được git.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RunTimeout);
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                var version = (await output).Trim();
                return process.ExitCode == 0
                    ? HealthCheckResult.Healthy("Git CLI sẵn sàng.", new Dictionary<string, object> { ["version"] = version })
                    : HealthCheckResult.Degraded($"git --version trả về mã {process.ExitCode}.");
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return HealthCheckResult.Degraded("git --version quá thời gian chờ.");
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return HealthCheckResult.Degraded("Không chạy được git. Máy chủ cần cài git và có trong PATH.", ex);
        }
    }
}
