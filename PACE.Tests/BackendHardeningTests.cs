using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PACE.API.Controllers;
using PACE.Application.QuestionGeneration;
using PACE.Infrastructure;
using PACE.Infrastructure.AI;
using PACE.Infrastructure.Services;
using Xunit;

namespace PACE.Tests;

public class BackendHardeningTests
{
    [Fact]
    public void QuestionsControllerRequiresStaffRole()
    {
        var authAttr = typeof(QuestionsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(Roles.Staff, authAttr.Roles);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpstashRedisOtpCacheServiceFailsClosedWhenConnectionStringMissing(string? connectionString)
    {
        var dict = new Dictionary<string, string?>();
        if (connectionString != null)
        {
            dict["Redis:ConnectionString"] = connectionString;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new UpstashRedisOtpCacheService(config));
        Assert.Contains("Redis:ConnectionString is not configured", ex.Message);
    }

    [Fact]
    public void DependencyInjectionResolvesGeminiWhenConfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=test",
                ["QuestionGeneration:Provider"] = "Gemini",
                ["Gemini:ApiKey"] = "fake-gemini-key",
                ["OpenAI:ApiKey"] = "fake-openai-key"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IQuestionLlmClient>();
        Assert.IsType<GeminiQuestionLlmClient>(client);
    }

    [Fact]
    public void DependencyInjectionResolvesOpenAiWhenConfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=test",
                ["QuestionGeneration:Provider"] = "OpenAI",
                ["Gemini:ApiKey"] = "fake-gemini-key",
                ["OpenAI:ApiKey"] = "fake-openai-key"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(config);
        using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IQuestionLlmClient>();
        Assert.IsType<OpenAiQuestionLlmClient>(client);
    }
}
