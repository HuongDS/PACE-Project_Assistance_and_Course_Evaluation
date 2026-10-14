using System.Text.Json;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Domain.Enums;
using Xunit;

namespace PACE.Tests;

public class BusinessTemplateGeneratorTests
{
    private static ParsedMethod Method(string cls, string name, string? verb, string? route, bool isAbstract = false, int line = 1)
        => new()
        {
            ClassName = cls, MethodName = name, FilePath = $"{cls}.cs", Language = ProgrammingLanguage.CSharp,
            SourceCode = "", HttpVerb = verb, Route = route, IsAbstract = isAbstract, StartLine = line
        };

    private static readonly List<TemplateEndpoint> Endpoints = BusinessTemplateGenerator.ExtractEndpoints([
        Method("AuthController", "Login", "POST", "/api/auth/login"),
        Method("AuthController", "Register", "POST", "/api/auth/register", line: 2),
        Method("BudgetController", "Create", "POST", "/api/budget"),
        Method("BudgetController", "GetAll", "GET", "/api/budget", line: 2)
    ]);

    [Fact]
    public void Extracts_only_concrete_endpoints_without_duplicates()
    {
        var endpoints = BusinessTemplateGenerator.ExtractEndpoints([
            Method("BudgetController", "Create", "post", "/api/budget"),
            Method("BudgetController", "Create", "POST", "/api/budget"),
            Method("BudgetController", "Helper", null, null),
            Method("IBudgetController", "Create", "POST", "/api/budget", isAbstract: true)
        ]);

        var endpoint = Assert.Single(endpoints);
        Assert.Equal(("BudgetController", "POST", "Create", "/api/budget"), (endpoint.Controller, endpoint.HttpMethod, endpoint.Function, endpoint.Path));
    }

    [Fact]
    public void Expands_aspnet_route_tokens()
    {
        var endpoint = Assert.Single(BusinessTemplateGenerator.ExtractEndpoints([
            Method("AnalysisController", "Analyze", "POST", "/api/[controller]/[action]")
        ]));
        Assert.Equal("/api/analysis/analyze", endpoint.Path);
    }

    [Fact]
    public void Groups_by_controller_with_readable_names()
    {
        var result = BusinessTemplateGenerator.GroupByController(BusinessTemplateGenerator.ExtractEndpoints([
            Method("UserProfileController", "Me", "GET", "/api/users/me"),
            Method("budget_items", "List", "GET", "/budget-items")
        ]));

        Assert.Equal(["User Profile", "budget items"], result.Select(b => b.BusinessName).OrderByDescending(n => n));
    }

    [Fact]
    public void Ai_grouping_ignores_unknown_and_duplicate_ids_and_keeps_missing_endpoints()
    {
        var warnings = new List<string>();
        var result = BusinessTemplateGenerator.ApplyGrouping(Endpoints, [
            new SuggestedBusiness("Xác thực", "Đăng nhập và đăng ký.", [0, 1, 99]),
            new SuggestedBusiness("Trùng", null, [1]),
            new SuggestedBusiness("Ngân sách", null, [2])
        ], warnings);

        Assert.Equal(["Xác thực", "Ngân sách", "Budget"], result.Select(b => b.BusinessName));
        Assert.Equal(2, result[0].Apis.Count);
        Assert.Equal("GetAll", Assert.Single(result[2].Apis).Function);
        Assert.Equal(2, warnings.Count);
        Assert.Equal(Endpoints.Count, result.Sum(b => b.Apis.Count));
    }

    [Fact]
    public void Serialized_template_round_trips_through_the_loader()
    {
        var generated = BusinessTemplateGenerator.GroupByController(Endpoints);

        var json = BusinessTemplateGenerator.Serialize(generated);
        var loaded = BusinessTemplateLoader.Parse(json);

        Assert.Equal(generated.Count, loaded.Count);
        var login = loaded.Single(b => b.BusinessName == "Auth").Apis[0];
        Assert.Equal(("Login", "POST"), (login.ResolveFunctionName(), login.ResolveHttpVerb()));
        using var doc = JsonDocument.Parse(json);
        var api = doc.RootElement[0].GetProperty("apis")[0];
        Assert.False(api.TryGetProperty("method", out _));
        Assert.False(api.TryGetProperty("DisplayName", out _));
    }

    [Fact]
    public void Normalize_merges_duplicate_names_and_drops_incomplete_apis()
    {
        var result = BusinessTemplateLoader.Normalize([
            new BusinessTemplate { BusinessName = " Budget ", Apis = [new ApiTemplate { Controller = "B", Function = "A" }] },
            new BusinessTemplate { BusinessName = "budget", Apis = [new ApiTemplate { Controller = "B", Function = "C" }, new ApiTemplate { Controller = "" }] },
            new BusinessTemplate { BusinessName = "  " }
        ]);

        var business = Assert.Single(result);
        Assert.Equal("Budget", business.BusinessName);
        Assert.Equal(2, business.Apis.Count);
    }
}
