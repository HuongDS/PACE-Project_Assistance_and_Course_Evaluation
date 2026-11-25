using PACE.Application.Analysis.Models;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;

namespace PACE.Tests;

public sealed class JavaMethodFlowRegressionTests
{
    [Fact]
    public void BreakAndContinueKeepTheirTargetsAndSourceLocations()
    {
        const string source = """
            class Demo {
                int measure(int n) {
                    while (true) {
                        if (n == 0) break;
                        n--;
                        continue;
                    }
                    return n;
                }
            }
            """;
        var flow = Assert.IsType<MethodFlowAnalysis>(Parse(source).JavaFlow);
        Assert.Equal(MethodFlowStatus.Complete, flow.Status);
        var nodes = flow.Nodes.ToDictionary(node => node.Id);
        var exit = Assert.Single(flow.Edges, edge => edge.Kind == MethodFlowEdgeKind.Break);
        Assert.Equal(8, nodes[exit.To].Site.Line);
        var restart = Assert.Single(flow.Edges, edge => edge.Kind == MethodFlowEdgeKind.Continue);
        Assert.Equal(3, nodes[restart.To].Site.Line);
        Assert.DoesNotContain(flow.Edges, edge => edge.From == restart.To && edge.Kind == MethodFlowEdgeKind.False);
        Assert.Contains(flow.DefUses, fact => fact.Variable == "n" && fact.Definition.Line == 5 && fact.Use.Line == 8);

        var stored = flow.Serialize();
        Assert.Equal(stored, MethodFlowAnalysis.Deserialize(stored)?.Serialize());
        Assert.Null(MethodFlowAnalysis.Deserialize(null));
    }

    [Fact]
    public void OmittedEvidenceDoesNotHideMethodCalls()
    {
        var source = "class Demo { int measure(int n) {" + string.Concat(Enumerable.Repeat("n++;", 260))
            + " System.out.println(n); return n; } }";
        var method = Parse(source);
        var flow = Assert.IsType<MethodFlowAnalysis>(method.JavaFlow);
        Assert.Equal(MethodFlowStatus.LimitReached, flow.Status);
        Assert.Equal("cfg-nodes", flow.Reason);
        Assert.Empty(flow.Nodes);
        Assert.Empty(flow.DefUses);
        Assert.Single(method.Calls, call => call.MethodName == "println");
    }

    private static ParsedMethod Parse(string source) =>
        Assert.Single(new JavaTreeSitterParser().Parse([new("Demo.java", "Demo.java", source)], default).Methods);
}
