using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using PACE.Application.Analysis;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using Xunit;

namespace PACE.Tests;

public class StoredBusinessTemplateTests
{
    private const string RepoUrl = "https://github.com/owner/repo";

    private static List<BusinessTemplate> Template(string name = "Ngân sách") =>
    [
        new BusinessTemplate
        {
            BusinessName = name,
            Description = "Quản lý ngân sách.",
            Apis = [new ApiTemplate { Controller = "BudgetController", HttpMethod = "POST", Function = "Create", Path = "/api/budget" }]
        }
    ];

    /// <summary>Giả lập Postgres jsonb: bỏ khoảng trắng và sắp lại thứ tự key (theo độ dài, như jsonb).</summary>
    private static string AsJsonb(string json)
    {
        static JsonNode? Reorder(JsonNode? node) => node switch
        {
            JsonObject o => new JsonObject(o.OrderBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => KeyValuePair.Create(p.Key, Reorder(p.Value?.DeepClone())))),
            JsonArray a => new JsonArray(a.Select(x => Reorder(x?.DeepClone())).ToArray()),
            _ => node?.DeepClone()
        };
        return Reorder(JsonNode.Parse(json))!.ToJsonString();
    }

    [Fact]
    public void Save_does_not_bump_timestamp_when_only_jsonb_formatting_differs()
    {
        var run = new AnalysisRun { Id = Guid.NewGuid() };
        StoredBusinessTemplate.Save(run, Template(), BusinessTemplateSource.Confirmed);
        var savedAt = run.TemplateUpdatedAt;
        Assert.NotNull(savedAt);
        Assert.Equal(0, savedAt!.Value.Ticks % 10);

        run.BusinessTemplateJson = AsJsonb(run.BusinessTemplateJson!);
        run.TemplateUpdatedAt = savedAt!.Value.AddMinutes(-5);
        StoredBusinessTemplate.Save(run, Template(), BusinessTemplateSource.Confirmed);
        Assert.Equal(savedAt.Value.AddMinutes(-5), run.TemplateUpdatedAt);

        StoredBusinessTemplate.Save(run, Template("Ngân sách mới"), BusinessTemplateSource.Confirmed);
        Assert.True(run.TemplateUpdatedAt > savedAt.Value.AddMinutes(-5));
        Assert.Equal("Ngân sách mới", StoredBusinessTemplate.Load(run)![0].BusinessName);
    }

    [Fact]
    public async Task Generate_returns_confirmed_template_from_db_without_cloning()
    {
        var run = new AnalysisRun { Id = Guid.NewGuid(), RepoUrl = RepoUrl };
        StoredBusinessTemplate.Save(run, Template(), BusinessTemplateSource.Confirmed);
        var cloned = false;
        var service = GenerationService(run, () => cloned = true);

        var result = await service.GenerateAsync(new GenerateTemplateRequest { RepoUrl = RepoUrl + ".git" });

        Assert.False(cloned);
        Assert.Equal(BusinessTemplateSource.Confirmed, result.Source);
        Assert.Equal("Ngân sách", Assert.Single(result.Businesses).BusinessName);
    }

    [Theory]
    [InlineData(BusinessTemplateSource.Confirmed, true)]
    [InlineData(BusinessTemplateSource.Repository, false)]
    public async Task Generate_clones_when_regenerating_or_template_not_confirmed(BusinessTemplateSource source, bool regenerate)
    {
        var run = new AnalysisRun { Id = Guid.NewGuid(), RepoUrl = RepoUrl };
        StoredBusinessTemplate.Save(run, Template(), source);
        var cloned = false;
        var service = GenerationService(run, () => cloned = true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync(new GenerateTemplateRequest { RepoUrl = RepoUrl, Regenerate = regenerate }));
        Assert.True(cloned);
    }

    [Fact]
    public async Task SaveTemplate_normalizes_and_marks_confirmed()
    {
        var run = new AnalysisRun { Id = Guid.NewGuid(), RepoUrl = RepoUrl };
        var saved = 0;
        var currentUser = Stub<ICurrentUserService>.Create((m, _) => m.Name switch
        {
            "get_IsAuthenticated" => true,
            "get_Role" => UserRole.Admin,
            _ => throw new NotSupportedException(m.Name)
        });
        var service = new AnalysisRunService(UnitOfWork(run, () => saved++),
            currentUser,
            Stub<IBusinessService>.Create((m, _) => throw new NotSupportedException(m.Name)));

        var result = await service.SaveTemplateAsync(run.Id, new BusinessTemplateRequest
        {
            Businesses = [.. Template(" Ngân sách "), .. Template("ngân SÁCH")]
        });

        Assert.Equal(1, saved);
        Assert.Equal(BusinessTemplateSource.Confirmed, run.TemplateSource);
        Assert.Equal((1, 2), (result.BusinessCount, result.ApiCount));
        Assert.Equal("Ngân sách", (await service.GetTemplateAsync(run.Id)).Businesses[0].BusinessName);
    }

    private static TemplateGenerationService GenerationService(AnalysisRun run, Action onClone)
        => new(UnitOfWork(run, () => { }),
            Stub<IGitCloneService>.Create((_, _) => { onClone(); throw new InvalidOperationException("clone"); }),
            Stub<IRepositoryScanner>.Create((m, _) => throw new NotSupportedException(m.Name)),
            [],
            Stub<IBusinessGroupingAi>.Create((m, _) => throw new NotSupportedException(m.Name)),
            NullLogger<TemplateGenerationService>.Instance);

    private static IUnitOfWork UnitOfWork(AnalysisRun run, Action onSave)
    {
        var runs = Stub<IAnalysisRunRepository>.Create((m, a) => m.Name switch
        {
            "GetByRepoUrlAsync" => Task.FromResult<AnalysisRun?>((string)a[0]! == run.RepoUrl ? run : null),
            "GetByIdAsync" => Task.FromResult<AnalysisRun?>((Guid)a[0]! == run.Id ? run : null),
            _ => throw new NotSupportedException(m.Name)
        });
        return Stub<IUnitOfWork>.Create((m, _) => m.Name switch
        {
            "get_AnalysisRuns" => runs,
            "SaveChangesAsync" => Task.FromResult(Run(onSave)),
            _ => throw new NotSupportedException(m.Name)
        });
    }

    private static int Run(Action action) { action(); return 1; }
}
