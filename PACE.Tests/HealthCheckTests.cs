using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PACE.API.HealthChecks;
using PACE.Application.Analysis;
using PACE.Application.Common.Interfaces;
using PACE.Infrastructure.Persistence;
using Xunit;

namespace PACE.Tests;

public class HealthCheckTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static Task<HealthCheckResult> Run(IHealthCheck check) => check.CheckHealthAsync(new HealthCheckContext());

    // ---- AI providers ----

    [Fact]
    public async Task AiProviders_Healthy_WhenRequiredKeysConfigured_AndGroqIsOptional()
    {
        var result = await Run(new AiProvidersHealthCheck(Config(
            ("Gemini:ApiKey", "g-secret"), ("DeepSeekConfig:ApiKey", "d-secret"))));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("missing", result.Data["Groq"]);
        Assert.Equal("missing", result.Data["OpenAI"]);
    }

    [Fact]
    public async Task AiProviders_Degraded_WhenDeepSeekMissing()
    {
        var result = await Run(new AiProvidersHealthCheck(Config(("Gemini:ApiKey", "g-secret"))));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("DeepSeek", result.Description);
    }

    [Fact]
    public async Task AiProviders_RequiresOpenAi_WhenItIsTheQuestionProvider()
    {
        var result = await Run(new AiProvidersHealthCheck(Config(
            ("QuestionGeneration:Provider", "OpenAI"),
            ("Gemini:ApiKey", "g-secret"), ("DeepSeekConfig:ApiKey", "d-secret"))));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("OpenAI", result.Description);
        Assert.Equal("OpenAI", result.Data["questionProvider"]);
    }

    [Fact]
    public async Task AiProviders_NeverExposesKeyValues()
    {
        var result = await Run(new AiProvidersHealthCheck(Config(
            ("Gemini:ApiKey", "g-secret"), ("DeepSeekConfig:ApiKey", "d-secret"), ("GroqConfig:ApiKey", "q-secret"))));

        var everything = result.Description + string.Join(",", result.Data.Select(d => $"{d.Key}={d.Value}"));
        Assert.DoesNotContain("secret", everything);
    }

    // ---- Redis ----

    private sealed class FakeOtpCache(Func<Task<string?>> get) : IOtpCacheService
    {
        public Task SetOtpAsync(string email, string otpCode, TimeSpan expiry, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> GetOtpAsync(string email, CancellationToken ct = default) => get();
        public Task RemoveOtpAsync(string email, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static RedisHealthCheck RedisCheck(IOtpCacheService? cache, bool configured = true)
    {
        var services = new ServiceCollection();
        if (cache is not null) services.AddSingleton(cache);
        return new RedisHealthCheck(services.BuildServiceProvider(),
            Config(("Redis:ConnectionString", configured ? "host:6379,password=x" : "")));
    }

    [Fact]
    public async Task Redis_Degraded_WhenNotConfigured()
    {
        var result = await Run(RedisCheck(null, configured: false));
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Redis_Healthy_WhenCacheResponds()
    {
        var result = await Run(RedisCheck(new FakeOtpCache(() => Task.FromResult<string?>(null))));
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Redis_Degraded_WhenCacheThrows_WithoutLeakingMessage()
    {
        var result = await Run(RedisCheck(new FakeOtpCache(() => throw new InvalidOperationException("host=secret.redis.io password=hunter2"))));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.DoesNotContain("hunter2", result.Description);
    }

    [Fact]
    public async Task Redis_Degraded_WhenCacheNeverResponds()
    {
        var result = await Run(RedisCheck(new FakeOtpCache(() => new TaskCompletionSource<string?>().Task)));
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    // ---- Git ----

    [Fact]
    public async Task Git_Degraded_WhenExecutableMissing()
    {
        var options = Options.Create(new AnalysisOptions { GitExecutable = "pace-git-does-not-exist" });
        var result = await Run(new GitCliHealthCheck(options));
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Git_Healthy_WhenGitIsInstalled()
    {
        var result = await Run(new GitCliHealthCheck(Options.Create(new AnalysisOptions())));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("git version", result.Data["version"].ToString());
    }

    // ---- PostgreSQL ----

    [Fact]
    public async Task Database_Unhealthy_WhenServerUnreachable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=pace;Username=u;Password=p;Timeout=2")
            .Options;
        await using var db = new AppDbContext(options);

        var result = await Run(new DatabaseHealthCheck(db));

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("127.0.0.1", result.Description);
    }

    // ---- Registration và response ----

    [Fact]
    public void AddPaceHealthChecks_RegistersFourDependencyChecks()
    {
        var services = new ServiceCollection();
        services.AddPaceHealthChecks();

        var registrations = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        Assert.Equal(["ai-providers", "git", "postgresql", "redis"], registrations.Select(r => r.Name).Order().ToArray());
        Assert.All(registrations, r => Assert.Contains(HealthCheckExtensions.DependencyTag, r.Tags));
        Assert.Equal(HealthStatus.Unhealthy, registrations.Single(r => r.Name == "postgresql").FailureStatus);
        Assert.All(registrations.Where(r => r.Name != "postgresql"), r => Assert.Equal(HealthStatus.Degraded, r.FailureStatus));
    }

    [Fact]
    public async Task DetailedReport_ListsChecks_ButNeverTheException()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["postgresql"] = new(HealthStatus.Unhealthy, "Không kết nối được PostgreSQL.",
                TimeSpan.FromMilliseconds(12), new InvalidOperationException("Host=db.internal;Password=hunter2"), null),
            ["ai-providers"] = new(HealthStatus.Healthy, "ok", TimeSpan.FromMilliseconds(1), null,
                new Dictionary<string, object> { ["Gemini"] = "configured" })
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(15));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthCheckExtensions.WriteDetailedReportAsync(context, report);

        context.Response.Body.Position = 0;
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Unhealthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("checks").GetArrayLength());
        Assert.DoesNotContain("hunter2", json);
        Assert.DoesNotContain("db.internal", json);
        Assert.Contains("configured", json);
    }
}
