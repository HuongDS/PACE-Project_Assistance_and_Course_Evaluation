using System.Text.Json;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;

namespace PACE.Tests;

public sealed class SpringRouteBenchmarkTests
{
    [Fact]
    public void PreservesRouteConstraintsAndBoundsAliasExpansion()
    {
        var aliases = string.Join(", ", Enumerable.Range(0, 65).Select(index => $"\"/route{index}\""));
        var source = $$$"""
            import org.springframework.web.bind.annotation.*;
            @RestController
            class Patterns {
                @GetMapping("/products/{id:\\d{2}}") public void constrained() {}
            }
            @RestController
            @RequestMapping({{{ "{" + aliases + "}" }}})
            class AliasLimit {
                @GetMapping({{{ "{" + aliases + "}" }}}) public void tooMany() {}
            }
            """;
        var result = new JavaTreeSitterParser().Parse([new("Patterns.java", "Patterns.java", source)], default);
        var matcher = new EntryPointMatcher(result.Methods);
        Assert.Equal("constrained", matcher.Match(new ApiTemplate
            { HttpMethod = "GET", Path = @"/products/{number:\d{2}}" }, out _)?.MethodName);
        Assert.Null(matcher.Match(new ApiTemplate { HttpMethod = "GET", Path = "/products/{number}" }, out _));
        Assert.DoesNotContain(BusinessTemplateGenerator.ExtractEndpoints(result.Methods), endpoint => endpoint.Function == "tooMany");
        Assert.Contains(result.Warnings, warning => warning.StartsWith("Spring:", StringComparison.Ordinal));
    }

    [Fact]
    public void DiscoversRoutesAndMatchesHandlers()
    {
        var source = """
            package demo;
            import org.springframework.web.bind.annotation.*;
            @RestController
            @RequestMapping(path = {"/v1", "/v2"})
            class Catalog {
                @RequestMapping(value = {"/items", "/stock"}, method = {RequestMethod.GET, RequestMethod.HEAD})
                public void inventory() {}
                @GetMapping("/Case") public void exactCase() {}
                @PostMapping("/job") public void create() {}
                @RequestMapping("/job") public void anyJob() {}
                @GetMapping("/conflict") public void conflictOne() {}
                @GetMapping("/conflict") public void conflictTwo() {}
                @GetMapping("/animals/{id}") public void animal() {}
                @GetMapping("/custom") public void custom() {}
                @GetMapping(path = UNKNOWN) public void dynamicValue() {}
            }
            @RequestMapping("/contract")
            interface Contract {
                @GetMapping("/lookup") void lookup();
            }
            @RestController class Implementation implements Contract {
                @Override public void lookup() {}
            }
            """;
        var fake = """
            package fake;
            @interface GetMapping { String value(); }
            class Unrelated { @GetMapping("/fake") void fake() {} }
            """;
        var result = new JavaTreeSitterParser().Parse(
            [new("Catalog.java", "Catalog.java", source), new("Unrelated.java", "Unrelated.java", fake)], default);
        var discovery = BusinessTemplateGenerator.ExtractEndpoints(result.Methods);
        var matcher = new EntryPointMatcher(result.Methods);
        var cases = new (string Key, string Verb, string Path, string Controller, string? Function, string? Expected)[]
        {
            ("base-and-path-aliases", "HEAD", "/v2/stock", "", null, "inventory"),
            ("all-explicit-verbs", "GET", "/v1/items", "", null, "inventory"),
            ("missing-verb", "DELETE", "/v1/items", "", null, null),
            ("case-sensitive", "GET", "/v1/case", "", null, null),
            ("exact-case", "GET", "/v1/Case", "", null, "exactCase"),
            ("route-over-name", "POST", "/v1/job", "Catalog", "custom", "create"),
            ("explicit-over-any", "POST", "/v2/job", "", null, "create"),
            ("any-fallback", "PUT", "/v2/job", "", null, "anyJob"),
            ("ambiguous-route", "GET", "/v1/conflict", "", null, null),
            ("named-ambiguity", "GET", "/v1/conflict", "Catalog", "conflictTwo", "conflictTwo"),
            ("variable-alias", "GET", "/v2/animals/{identifier}", "", null, "animal"),
            ("no-suffix-fallback", "GET", "/wrong/v1/Case", "Catalog", "exactCase", null),
            ("skip-unresolved-path", "GET", "/v1", "", null, null),
            ("inherited-mapping", "GET", "/contract/lookup", "", null, "lookup"),
            ("recognize-spring-annotation", "GET", "/fake", "", null, null)
        };
        var rows = cases.Select(test =>
        {
            var api = new ApiTemplate { HttpMethod = test.Verb, Path = test.Path, Controller = test.Controller, Function = test.Function };
            var method = matcher.Match(api, out var note);
            return new { test.Key, test.Expected, Observed = method?.MethodName, Note = note };
        }).ToArray();
        var knownRoutes = discovery.Select(endpoint => $"{endpoint.HttpMethod} {endpoint.Path}").Distinct().Order().ToArray();
        var report = Environment.GetEnvironmentVariable("PACE_SPRING_ROUTES_REPORT");
        if (!string.IsNullOrEmpty(report))
            File.WriteAllText(report, JsonSerializer.Serialize(new { rows, knownRoutes }, new JsonSerializerOptions { WriteIndented = true }));
        if (Environment.GetEnvironmentVariable("PACE_SPRING_ROUTES_BASELINE") == "1") return;
        Assert.All(rows, row => Assert.Equal(row.Expected, row.Observed));
        Assert.Equal(8, knownRoutes.Count(route => route.EndsWith("/items") || route.EndsWith("/stock")));
        Assert.DoesNotContain("GET /fake", knownRoutes);
    }
}
