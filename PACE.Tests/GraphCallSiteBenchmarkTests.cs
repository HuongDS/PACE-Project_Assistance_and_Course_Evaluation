using System.Text.Json;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Models;
using PACE.Domain.Enums;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;

namespace PACE.Tests;

public sealed class GraphCallSiteBenchmarkTests
{
    [Fact]
    public void MeasuresCallOccurrencesAndTraversalLimits()
    {
        const string code = """
            class Orders {
                void repeated() {
                    check();
                    check(); check();
                }
                void check() {}
                void recurse() { recurse(); }
                void mutualFirst() { mutualSecond(); }
                void mutualSecond() { mutualFirst(); }
                void shallow() { check(); }
                void deep() { longPath(); shared(); }
                void longPath() { shared(); }
                void shared() { check(); }
                int value() { return 1; }
                void returnBudget() { value(); }
            }
            """;
        var parsed = new JavaTreeSitterParser().Parse([new("Orders.java", "Orders.java", code)], default);
        var resolver = new CallResolver(parsed.Classes, parsed.Methods);
        FlowGraph Flow(string entry, int depth = 12, int steps = 250)
            => new FlowBuilder(resolver, depth, steps).Build(Assert.Single(parsed.Methods, m => m.MethodName == entry));

        var scenarios = new (string Id, FlowGraph Graph, int Calls, int Returns, bool Truncated)[]
        {
            ("repeated-sites", Flow("repeated"), 3, 0, false),
            ("direct-recursion", Flow("recurse"), 1, 0, false),
            ("mutual-recursion", Flow("mutualFirst"), 2, 0, false),
            ("depth-limit-at-leaf", Flow("shallow", depth: 1), 1, 0, false),
            ("depth-limit-with-work", Flow("deep", depth: 1), 2, 0, true),
            ("shorter-path-reaches-shared-method", Flow("deep", depth: 2), 4, 0, false),
            ("step-budget-omits-return", Flow("returnBudget", steps: 1), 1, 0, true),
        };
        var report = scenarios.Select(s => new
        {
            s.Id,
            Expected = new { s.Calls, s.Returns, s.Truncated },
            Observed = new
            {
                Calls = s.Graph.Steps.Count(step => step.EdgeType == FlowEdgeType.Call),
                Returns = s.Graph.Steps.Count(step => step.EdgeType == FlowEdgeType.Return),
                s.Graph.Truncated
            }
        }).ToArray();
        var path = Environment.GetEnvironmentVariable("PACE_GRAPH_CALL_SITE_REPORT");
        if (!string.IsNullOrWhiteSpace(path))
            File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        if (Environment.GetEnvironmentVariable("PACE_GRAPH_CALL_SITE_BASELINE") == "1") return;

        Assert.All(report, item => Assert.Equal(item.Expected, item.Observed));
        var repeated = scenarios[0].Graph;
        Assert.Equal(new CallSite?[] { new(3, 9), new(4, 9), new(4, 18) }, repeated.Steps.Select(step => step.Site));
        Assert.Contains("dòng 4:18", MermaidBuilder.Build(repeated));
        var recursive = Assert.Single(scenarios[1].Graph.Steps);
        Assert.Same(recursive.From, recursive.To);
        Assert.Equal(FlowTruncationReason.DepthLimit, scenarios[4].Graph.TruncationReason);
        Assert.Equal(FlowTruncationReason.StepLimit, scenarios[6].Graph.TruncationReason);
        Assert.Contains("giới hạn số bước", MermaidBuilder.Build(scenarios[6].Graph));
        var returned = Flow("returnBudget", steps: 2);
        Assert.Equal(FlowTruncationReason.None, returned.TruncationReason);
        Assert.Equal(returned.Steps[0].Site, returned.Steps[1].Site);
        Assert.Equal(FlowEdgeType.Return, returned.Steps[1].EdgeType);
    }

    [Fact]
    public void CapturesPhysicalColumnsAcrossParsers()
    {
        const string cs = "class A { void Entry() { /* é 😀 */ Check(); } void Check() {} }";
        const string ts = "function entry() { /* é 😀 */ check(); } function check() {}";
        const string java = "class A { void entry() { /* é 😀 */ check(); } void check() {} }";
        var entries = new (string Code, string Name, string Call, ParseResult Parsed)[]
        {
            (cs, "Entry", "Check();", new CSharpRoslynParser().Parse([new("A.cs", "A.cs", cs)], default)),
            (ts, "entry", "check();", new JsTsTreeSitterParser().Parse([new("a.ts", "a.ts", ts)], default)),
            (java, "entry", "check();", new JavaTreeSitterParser().Parse([new("A.java", "A.java", java)], default))
        };
        foreach (var entry in entries)
        {
            var method = Assert.Single(entry.Parsed.Methods, m => m.MethodName == entry.Name);
            var call = Assert.Single(method.Calls);
            Assert.Equal(new CallSite(1, entry.Code.IndexOf(entry.Call, StringComparison.Ordinal) + 1), call.Site);
        }
    }
}
