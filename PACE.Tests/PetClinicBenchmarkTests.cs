using System.Diagnostics;
using System.Text.Json;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Template;
using PACE.Infrastructure.Analysis;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;
using Xunit.Abstractions;

namespace PACE.Tests;

public sealed class PetClinicBenchmarkTests(ITestOutputHelper output)
{
    private sealed record Gold(string Revision, Endpoint[] Endpoints);
    private sealed record Endpoint(string Verb, string Route, string Path, int Line, string Method);

    [Fact]
    [Trait("Category", "ExternalBenchmark")]
    public void ReportSpringEndpointDiscovery()
    {
        var root = Environment.GetEnvironmentVariable("PACE_PETCLINIC_ROOT");
        var goldFile = Environment.GetEnvironmentVariable("PACE_PETCLINIC_GOLD");
        if (string.IsNullOrWhiteSpace(root) && string.IsNullOrWhiteSpace(goldFile))
        {
            output.WriteLine("Set PACE_PETCLINIC_ROOT and PACE_PETCLINIC_GOLD to run the external benchmark.");
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(root));
        Assert.False(string.IsNullOrWhiteSpace(goldFile));
        var gold = JsonSerializer.Deserialize<Gold>(File.ReadAllText(goldFile!),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.NotEmpty(gold.Endpoints);
        Assert.Equal(gold.Revision, Git(root!, "rev-parse", "HEAD"));
        Assert.Empty(Git(root!, "status", "--porcelain", "--untracked-files=all"));

        var scan = new RepositoryScanner().Scan(root!, default);
        Assert.NotEmpty(scan.JavaFiles);
        var parsed = new JavaTreeSitterParser().Parse(scan.JavaFiles, default);
        var discovered = BusinessTemplateGenerator.ExtractEndpoints(parsed.Methods);
        var matcher = new EntryPointMatcher(parsed.Methods);
        var rows = gold.Endpoints.Select(expected =>
        {
            var found = discovered.Any(endpoint => endpoint.HttpMethod == expected.Verb &&
                endpoint.Path == expected.Route && endpoint.Function == expected.Method &&
                endpoint.Controller == Path.GetFileNameWithoutExtension(expected.Path));
            var match = matcher.Match(new ApiTemplate { HttpMethod = expected.Verb, Path = expected.Route }, out var warning);
            var status = match is null ? "unresolved"
                : match.FilePath == expected.Path && match.MethodName == expected.Method ? "correct" : "wrong";
            return new { expected, discovered = found, status, actualFile = match?.FilePath, actualMethod = match?.MethodName, warning };
        }).ToArray();

        output.WriteLine($"Java files={scan.JavaFiles.Count}, methods={parsed.Methods.Count}, parser warnings={parsed.Warnings.Count}");
        output.WriteLine($"OpenAPI endpoint discovery={rows.Count(row => row.discovered)}/{rows.Length}");
        foreach (var status in rows.GroupBy(row => row.status))
            output.WriteLine($"Route-only mapping {status.Key}={status.Count()}/{rows.Length}");
        var report = Environment.GetEnvironmentVariable("PACE_PETCLINIC_REPORT");
        if (!string.IsNullOrWhiteSpace(report))
            File.WriteAllText(report, JsonSerializer.Serialize(rows,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }) + "\n");
    }

    private static string Git(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var result = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return result;
    }
}
