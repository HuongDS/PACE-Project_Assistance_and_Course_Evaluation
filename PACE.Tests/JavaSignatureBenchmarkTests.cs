using System.Diagnostics;
using System.Text.Json;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Models;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;

namespace PACE.Tests;

public sealed class JavaSignatureBenchmarkTests
{
    private sealed record ReferenceCase(string Id, string Caller, string[] Expected);
    private sealed record ReferenceData(Dictionary<string, string> Sources, ReferenceCase[] Cases);
    private sealed record Score(string Id, string[] Expected, string[] Observed);

    [Fact]
    [Trait("Category", "ExternalBenchmark")]
    public void ResolvesJavaCallsBySignature()
    {
        var fixture = Environment.GetEnvironmentVariable("PACE_JAVA_SIGNATURE_FIXTURE");
        if (string.IsNullOrWhiteSpace(fixture)) return;

        var data = JsonSerializer.Deserialize<ReferenceData>(File.ReadAllText(fixture),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var files = data.Sources.Select(item => new SourceFile(item.Key, item.Key, item.Value)).ToArray();
        var watch = Stopwatch.StartNew();
        var parsed = new JavaTreeSitterParser().Parse(files, default);
        var resolver = new CallResolver(parsed.Classes, parsed.Methods);
        var scores = data.Cases.Select(test =>
        {
            var caller = Assert.Single(parsed.Methods, method => method.MethodName == test.Caller);
            var targets = new FlowBuilder(resolver).Build(caller).Steps
                .Where(step => step.From == caller && step.EdgeType == PACE.Domain.Enums.FlowEdgeType.Call)
                .Select(step => step.To)
                .DistinctBy(target => target.Id)
                .Select(target => $"{target.FilePath}:{target.MethodName}({string.Join(",", target.Parameters.Select(parameter => parameter.Type))})")
                .Order(StringComparer.Ordinal).ToArray();
            return new Score(test.Id, test.Expected.Order(StringComparer.Ordinal).ToArray(), targets);
        }).ToArray();

        var report = Environment.GetEnvironmentVariable("PACE_JAVA_SIGNATURE_REPORT");
        if (!string.IsNullOrWhiteSpace(report))
            File.WriteAllText(report, JsonSerializer.Serialize(new { scores, elapsedMs = watch.Elapsed.TotalMilliseconds, parsed.Warnings },
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

        if (Environment.GetEnvironmentVariable("PACE_JAVA_SIGNATURE_BASELINE") != "1")
            Assert.All(scores, score => Assert.Equal(score.Expected, score.Observed));
    }
}
