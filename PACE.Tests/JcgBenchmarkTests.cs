using System.Text.Json;
using System.Text.RegularExpressions;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Models;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;
using Xunit.Abstractions;

namespace PACE.Tests;

public sealed class JcgBenchmarkTests(ITestOutputHelper output)
{
    private const string Revision = "4737aac8c2652acded1c4505961b6af52d06ceeb";
    private sealed record Dataset(string Revision, Case[] Cases);
    private sealed record Case(string Id, SourceFileInput[] Files, Annotation[] Checks);
    private sealed record SourceFileInput(string Path, string Source);
    private sealed record Annotation(string File, string Kind, int Line, int AnnotationLine, string Method,
        string[] ResolvedTargets, string[] ProhibitedTargets);
    private sealed record Score(string Case, string File, int Line, int AnnotationLine, string Method, string Status,
        string[] Required, string[] Prohibited, string[] Observed);

    [Fact]
    [Trait("Category", "ExternalBenchmark")]
    public void ReportJcgJava()
    {
        var input = Environment.GetEnvironmentVariable("PACE_JCG_DATASET");
        if (string.IsNullOrWhiteSpace(input))
        {
            output.WriteLine("Set PACE_JCG_DATASET to run the external benchmark.");
            return;
        }

        var dataset = JsonSerializer.Deserialize<Dataset>(File.ReadAllText(input),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(Revision, dataset.Revision);
        Assert.NotEmpty(dataset.Cases);

        List<Score> scores = [];
        foreach (var item in dataset.Cases)
        {
            var files = item.Files.Select(file => new SourceFile(file.Path, file.Path, file.Source)).ToList();
            var parsed = new JavaTreeSitterParser().Parse(files, default);
            var resolver = new CallResolver(parsed.Classes, parsed.Methods);
            var sourceByPath = files.ToDictionary(file => file.RelativePath);
            string Owner(ParsedMethod method)
            {
                var package = Regex.Match(sourceByPath[method.FilePath].Content, @"(?m)^\s*package\s+([\w.]+)\s*;");
                return package.Success ? $"{package.Groups[1].Value}.{method.ClassName}" : method.ClassName;
            }

            foreach (var check in item.Checks)
            {
                var status = "scored";
                string[] observed = [];
                var lines = sourceByPath[check.File].Content.Split('\n');
                var callPattern = @"\b" + Regex.Escape(check.Method) + @"\s*\(";
                if (check.Kind != "DirectCall" || check.Method.StartsWith('<')
                    || check.ResolvedTargets.Length == 0
                    || check.ResolvedTargets.Concat(check.ProhibitedTargets).Any(target => target.Contains('$')))
                {
                    status = "outside-direct-method-scope";
                }
                else if (check.Line < 1 || check.Line > lines.Length || !Regex.IsMatch(lines[check.Line - 1], callPattern))
                {
                    status = "no-source-invocation-at-annotated-line";
                }
                else if (parsed.Warnings.Count > 0)
                {
                    status = "parse-failure";
                }
                else
                {
                    var caller = parsed.Methods
                        .Where(method => method.FilePath == check.File && method.StartLine <= check.Line &&
                            check.Line <= method.StartLine + method.SourceCode.Count(character => character == '\n'))
                        .OrderByDescending(method => method.StartLine).FirstOrDefault();
                    if (caller is null)
                    {
                        status = "caller-missing";
                    }
                    else
                    {
                        // B0 exposes method-level relations. Avoid attributing another same-name invocation to this site.
                        var declarationOccurrences = caller.MethodName == check.Method ? 1 : 0;
                        if (Regex.Matches(caller.SourceCode, callPattern).Count != 1 + declarationOccurrences)
                        {
                            status = "ambiguous-method-level-projection";
                        }
                        else
                        {
                            observed = caller.Calls.Where(call => call.MethodName == check.Method)
                                .SelectMany(call => resolver.Resolve(call, caller))
                                .Select(Owner).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                        }
                    }
                }
                scores.Add(new(item.Id, check.File, check.Line, check.AnnotationLine, check.Method, status,
                    check.ResolvedTargets, check.ProhibitedTargets, observed));
            }
        }

        var measurable = scores.Where(row => row.Status is "scored" or "caller-missing" or "parse-failure").ToArray();
        Assert.NotEmpty(measurable);
        var required = measurable.Sum(row => row.Required.Length);
        var found = measurable.Sum(row => row.Required.Intersect(row.Observed).Count());
        var prohibited = measurable.Sum(row => row.Prohibited.Length);
        var violations = measurable.Sum(row => row.Prohibited.Intersect(row.Observed).Count());
        output.WriteLine($"Cases={dataset.Cases.Length}; annotation rows={scores.Count}; measurable={measurable.Length} in {measurable.Select(row => row.Case).Distinct().Count()} cases");
        foreach (var status in scores.GroupBy(row => row.Status).OrderBy(group => group.Key))
            output.WriteLine($"{status.Key}={status.Count()}");
        output.WriteLine($"Required targets found={found}/{required}; prohibited targets reported={violations}/{prohibited}");
        var report = Environment.GetEnvironmentVariable("PACE_JCG_REPORT");
        if (!string.IsNullOrWhiteSpace(report))
            File.WriteAllText(report, JsonSerializer.Serialize(scores,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }) + "\n");
    }
}
