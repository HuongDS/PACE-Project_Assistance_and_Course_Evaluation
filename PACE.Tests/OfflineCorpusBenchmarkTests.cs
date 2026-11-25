using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using PACE.Application.Analysis;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Infrastructure.Analysis;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;
using Xunit.Abstractions;

namespace PACE.Tests;

public sealed class OfflineCorpusBenchmarkTests(ITestOutputHelper output)
{
    private sealed record Input(RepositoryInput[] Repositories, GoldCase[] Cases);
    private sealed record RepositoryInput(string Id, string Commit, string Root);
    private sealed record GoldCase(string Id, string RepositoryId, string Verb, string Route);
    private sealed record Location(string File, int Line);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void ExportPinnedPublicCorpus()
    {
        var inputPath = Environment.GetEnvironmentVariable("PACE_OFFLINE_CORPUS_INPUT");
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            output.WriteLine("Run this benchmark through benchmarks/run.py.");
            return;
        }

        var reportPath = Environment.GetEnvironmentVariable("PACE_OFFLINE_CORPUS_REPORT");
        Assert.False(string.IsNullOrWhiteSpace(reportPath));
        var input = JsonSerializer.Deserialize<Input>(File.ReadAllText(inputPath), Json)!;
        Assert.NotEmpty(input.Repositories);
        var reports = new List<object>();
        List<string> failures = [];
        foreach (var repo in input.Repositories)
        {
            var clock = Stopwatch.StartNew();
            try
            {
                var report = Analyze(repo, input.Cases.Where(item => item.RepositoryId == repo.Id));
                reports.Add(report);
                output.WriteLine($"{repo.Id}: completed in {clock.ElapsedMilliseconds} ms");
            }
            catch (Exception exception)
            {
                failures.Add(repo.Id);
                reports.Add(new
                {
                    repo.Id, repo.Commit, Status = "failed", DurationMs = clock.ElapsedMilliseconds,
                    Error = exception.ToString()
                });
                output.WriteLine($"{repo.Id}: {exception.GetType().Name}");
            }
        }
        File.WriteAllText(reportPath!, JsonSerializer.Serialize(reports, Json) + "\n");
        Assert.Empty(failures);
    }

    private static object Analyze(RepositoryInput repo, IEnumerable<GoldCase> goldCases)
    {
        var options = new AnalysisOptions();
        var stopwatch = Stopwatch.StartNew();
        var scan = new RepositoryScanner().Scan(repo.Root, default);
        var parsed = new ParseResult();
        parsed.Merge(new CSharpRoslynParser().Parse(scan.CSharpFiles, default));
        parsed.Merge(new JavaTreeSitterParser().Parse(scan.JavaFiles, default));
        parsed.Merge(new JsTsTreeSitterParser().Parse(scan.JsTsFiles, default));
        parsed.Methods.AddRange(scan.FrontendEntries());

        var matcher = new EntryPointMatcher(parsed.Methods);
        var builder = new FlowBuilder(new CallResolver(parsed.Classes, parsed.Methods),
            options.MaxFlowDepth, options.MaxStepsPerFlow);

        object MakeFlow(string caseId, ApiTemplate api, string source)
        {
            var entry = matcher.Match(api, out var note);
            var graph = entry is null ? null : builder.Build(entry);
            return new
            {
                CaseId = caseId,
                Source = source,
                Verb = api.ResolveHttpVerb(),
                Route = api.Path,
                Hint = api.ResolveFunctionName(),
                Status = entry is null ? "unresolved" : "resolved",
                Note = note,
                Entry = entry is null ? null : GetLocation(entry),
                Truncated = graph?.Truncated,
                TruncationReason = graph?.TruncationReason,
                Nodes = graph?.Methods.OrderBy(method => method.FilePath, StringComparer.Ordinal)
                    .ThenBy(method => method.StartLine)
                    .Select(method => new
                    {
                        Location = GetLocation(method), method.ClassName, method.MethodName,
                        method.Language, method.IsDecision
                    }).ToArray(),
                Edges = graph?.Steps.Select(step => new
                {
                    step.Order, From = GetLocation(step.From), To = GetLocation(step.To),
                    step.EdgeType, step.Condition, step.Site
                }).ToArray(),
                Mermaid = graph is null ? null : MermaidBuilder.Build(graph)
            };
        }

        var endpoints = BusinessTemplateGenerator.ExtractEndpoints(parsed.Methods);
        var discovery = endpoints.Select(endpoint => MakeFlow($"discovered-{endpoint.Id}", endpoint.ToApi(), "discovery")).ToArray();
        var gold = goldCases.Select(item => MakeFlow(item.Id,
            new ApiTemplate { HttpMethod = item.Verb, Path = item.Route }, "gold")).ToArray();

        return new
        {
            repo.Id, repo.Commit,
            Status = scan.TotalFiles == 0 ? "no_supported_sources" : parsed.Methods.Count == 0 ? "no_methods" : "ok",
            DurationMs = stopwatch.ElapsedMilliseconds,
            Files = new { Total = scan.TotalFiles, Java = scan.JavaFiles.Count, CSharp = scan.CSharpFiles.Count, JsTs = scan.JsTsFiles.Count },
            scan.Stacks,
            ParsedMethodCount = parsed.Methods.Count,
            JavaMethodFlow = parsed.Methods.Select(method => method.JavaFlow).OfType<MethodFlowAnalysis>()
                .GroupBy(flow => (flow.Status, flow.Reason))
                .OrderBy(group => group.Key.Status).ThenBy(group => group.Key.Reason, StringComparer.Ordinal)
                .Select(group => new { group.Key.Status, group.Key.Reason, Count = group.Count() }).ToArray(),
            Warnings = scan.Warnings.Concat(parsed.Warnings).ToArray(),
            Options = new { options.MaxFlowDepth, options.MaxStepsPerFlow },
            Discovery = discovery,
            Gold = gold
        };
    }

    private static Location GetLocation(ParsedMethod method) => new(method.FilePath, method.StartLine);
}
