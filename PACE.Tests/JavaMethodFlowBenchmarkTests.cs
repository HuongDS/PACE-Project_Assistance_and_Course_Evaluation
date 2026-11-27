using System.Text.Json;
using System.Text.Json.Serialization;
using PACE.Application.Analysis.Models;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;

namespace PACE.Tests;

public sealed class JavaMethodFlowBenchmarkTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record Case(string Id, string Source, string Status, string[] Control, string[] Data);
    private sealed record Evidence(string Status, string[] Control, string[] Data);

    private static readonly Case[] Cases =
    [
        new("straight-line", """
            int measured(int n) {
                int x = n;
                x = 7;
                return x;
            }
            """, "Complete", ["Return:5>exit"], ["n:2>3", "x:4>5"]),
        new("if-else-join", """
            int measured(boolean flag) {
                int x;
                if (flag) { x = 1; }
                else { x = 2; }
                return x;
            }
            """, "Complete", ["False:4>5", "Return:6>exit", "True:4>4"],
            ["flag:2>4", "x:4>6", "x:5>6"]),
        new("early-return", """
            int measured(boolean stop) {
                int x = 1;
                if (stop) {
                    x = 2;
                    return x;
                }
                return x;
            }
            """, "Complete", ["False:4>8", "Return:6>exit", "Return:8>exit", "True:4>5"],
            ["stop:2>4", "x:3>8", "x:5>6"]),
        new("loop-back-edge", """
            int measured(int n) {
                int x = 0;
                while (n > 0) {
                    x += n;
                    n--;
                }
                return x;
            }
            """, "Complete", ["False:4>8", "Return:8>exit", "True:4>5"],
            ["n:2>4", "n:2>5", "n:2>6", "n:6>4", "n:6>5", "n:6>6",
             "x:3>5", "x:3>8", "x:5>5", "x:5>8"]),
        new("name-reused-in-disjoint-scopes", """
            int measured(boolean flag) {
                if (flag) {
                    int x = 1;
                    return x;
                }
                int x = 2;
                return x;
            }
            """, "Complete", ["False:3>7", "Return:5>exit", "Return:8>exit", "True:3>4"],
            ["flag:2>3", "x:4>5", "x:7>8"]),
        new("nested-branch-and-throw", """
            int measured(boolean first, boolean second, RuntimeException failure) {
                if (first) {
                    if (second) throw failure;
                    return 1;
                }
                return 2;
            }
            """, "Complete", ["False:3>7", "False:4>5", "Return:5>exit", "Return:7>exit",
             "Throw:4>exit", "True:3>4", "True:4>4"], ["failure:2>4", "first:2>3", "second:2>4"]),
        new("unsupported-try-finally", """
            int measured(int n) {
                try { return n; } finally { n++; }
            }
            """, "Unsupported", [], []),
        new("unsupported-nested-write", """
            int measured(int n) {
                if (n > 0 && ++n > 2) return n;
                return 0;
            }
            """, "Unsupported", [], [])
    ];

    [Fact]
    public void MeasuresControlAndReachingDefinitions()
    {
        var source = string.Join("\n", Cases.Select((item, index) => $"class Case{index} {{\n{item.Source}\n}}"));
        var parsed = new JavaTreeSitterParser().Parse([new("Cases.java", "Cases.java", source)], default);
        var rows = Cases.Select((item, index) =>
        {
            var method = Assert.Single(parsed.Methods, method => method.ClassName == $"Case{index}");
            var expected = new Evidence(item.Status, Sorted(item.Control), Sorted(item.Data));
            var observed = Observe(method);
            return new { item.Id, Expected = expected, Observed = observed,
                Correct = expected.Status == observed.Status && expected.Control.SequenceEqual(observed.Control)
                    && expected.Data.SequenceEqual(observed.Data) };
        }).ToArray();

        var path = Environment.GetEnvironmentVariable("PACE_JAVA_METHOD_FLOW_REPORT");
        if (!string.IsNullOrWhiteSpace(path))
            File.WriteAllText(path, JsonSerializer.Serialize(rows, Json) + "\n");
        if (Environment.GetEnvironmentVariable("PACE_JAVA_METHOD_FLOW_BASELINE") != "1")
            Assert.All(rows, row => Assert.True(row.Correct,
                $"{row.Id}: {JsonSerializer.Serialize(row.Observed, Json)}"));
    }

    private static Evidence Observe(ParsedMethod method)
    {
        // Read the exported schema on both revisions. B3 has no method-flow property.
        var flow = typeof(ParsedMethod).GetProperty("JavaFlow")?.GetValue(method);
        if (flow is null) return new Evidence("Unavailable", [], []);
        var json = JsonSerializer.SerializeToElement(flow, Json);
        var nodes = json.GetProperty("nodes").EnumerateArray().ToDictionary(n => n.GetProperty("id").GetInt32());
        int Line(JsonElement site) => site.GetProperty("line").GetInt32() - method.StartLine + 2;
        string Node(int id)
        {
            var node = nodes[id];
            return node.GetProperty("kind").GetString() is "Exit" ? "exit" : Line(node.GetProperty("site")).ToString();
        }
        var control = json.GetProperty("edges").EnumerateArray()
            .Where(edge => edge.GetProperty("kind").GetString() != "Next")
            .Select(edge => $"{edge.GetProperty("kind").GetString()}:{Node(edge.GetProperty("from").GetInt32())}>{Node(edge.GetProperty("to").GetInt32())}");
        var data = json.GetProperty("defUses").EnumerateArray()
            .Select(edge => $"{edge.GetProperty("variable").GetString()}:{Line(edge.GetProperty("definition"))}>{Line(edge.GetProperty("use"))}");
        return new Evidence(json.GetProperty("status").GetString()!, Sorted(control), Sorted(data));
    }

    private static string[] Sorted(IEnumerable<string> values) => values.Order(StringComparer.Ordinal).ToArray();
}
