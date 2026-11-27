using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace PACE.Infrastructure.Analysis.Evidence;

/// <summary>AST và CFG của một hàm C# đã lưu bằng Roslyn.</summary>
public static class CSharpStructure
{
    // Mã lưu trong DB chỉ là thân một hàm, không có using của file gốc: thêm các using mặc định để các kiểu BCL phổ biến vẫn phân giải được.
    private const string Prefix =
        "using System; using System.Collections.Generic; using System.Linq; using System.Text; using System.Threading; using System.Threading.Tasks; class __PaceFragment { ";

    private static readonly string[] ReferenceNames =
        ["System.Private.CoreLib", "System.Runtime", "System.Collections", "System.Linq", "System.Console", "System.Threading", "System.Threading.Tasks", "System.Text.RegularExpressions"];

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(() =>
    {
        var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var found = trusted
            .Where(p => ReferenceNames.Contains(Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        if (found.Count == 0) found.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        return found;
    });

    public static StructureOutcome BuildAst(Guid sourceId, string code, int nodeBudget, CancellationToken ct)
    {
        var root = SyntaxFactory.ParseMemberDeclaration(code);
        if (root is null || root.ContainsDiagnostics)
            return StructureOutcome.Fail("Persisted C# fragment cannot be parsed as a member; verified AST unavailable.");

        var all = root.DescendantNodesAndSelf().Take(nodeBudget + 1).ToArray();
        if (all.Length > nodeBudget) return StructureOutcome.Fail("AST exceeds 2000 node limit.");

        var ids = all.Select((n, i) => (n, id: $"{sourceId}:{i}")).ToDictionary(x => x.n, x => x.id);
        var nodes = new List<GraphNode>(all.Length);
        var edges = new List<GraphEdge>();
        foreach (var n in all)
        {
            ct.ThrowIfCancellationRequested();
            nodes.Add(new GraphNode(ids[n], n.Kind().ToString(), "statement"));
            if (n.Parent != null && ids.TryGetValue(n.Parent, out var parent)) edges.Add(new GraphEdge(parent, ids[n], "syntax-child"));
        }

        var complexity = CyclomaticComplexity.IsTruncated(code) ? (int?)null : CyclomaticComplexity.ForCSharp(root);
        return new StructureOutcome(nodes, edges, complexity, 0, null);
    }

    /// <summary>
    /// CFG bằng Roslyn. Hàm tham chiếu kiểu của project (không có trong mã đã lưu) vẫn được dựng CFG từ cú pháp;
    /// số lỗi ngữ nghĩa đó được báo lại trong <see cref="StructureOutcome.SemanticErrors"/> thay vì trả 422.
    /// Chỉ lỗi cú pháp (mã bị cắt, không phải một hàm hoàn chỉnh) mới làm CFG không dựng được.
    /// </summary>
    public static StructureOutcome BuildCfg(Guid sourceId, string methodName, string code, int nodeBudget, CancellationToken ct)
    {
        if (CyclomaticComplexity.IsTruncated(code))
            return StructureOutcome.Fail("Persisted C# method was truncated when stored (too long); verified CFG unavailable.");

        var tree = CSharpSyntaxTree.ParseText(Prefix + code + " }", cancellationToken: ct);
        if (tree.GetDiagnostics(ct).Any(d => d.Severity == DiagnosticSeverity.Error))
            return StructureOutcome.Fail("Persisted C# fragment has syntax errors; verified CFG unavailable.");

        var method = tree.GetRoot(ct).DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null) return StructureOutcome.Fail("Verified CFG supports method declarations only.");
        if (method.Body is null && method.ExpressionBody is null)
            return StructureOutcome.Fail("Persisted C# method has no body (abstract, interface or partial declaration); there is no control flow to graph.");

        var compilation = CSharpCompilation.Create("PaceEvidence", [tree], References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var model = compilation.GetSemanticModel(tree);

        ControlFlowGraph? cfg;
        try { cfg = ControlFlowGraph.Create(method, model, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StructureOutcome.Fail("Roslyn could not create a verified method CFG.");
        }
        if (cfg is null) return StructureOutcome.Fail("Roslyn could not create a verified method CFG.");
        if (cfg.Blocks.Length > nodeBudget) return StructureOutcome.Fail("CFG exceeds 2000 node limit.");

        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        foreach (var block in cfg.Blocks)
        {
            var id = $"{sourceId}:block:{block.Ordinal}";
            var label = $"{methodName} / {block.Kind} #{block.Ordinal}: " + string.Join("; ", block.Operations.Select(o => o.Syntax.ToString()));
            if (block.BranchValue is not null) label += "; condition/value: " + block.BranchValue.Syntax;
            nodes.Add(new GraphNode(id, label, "statement"));
            if (block.FallThroughSuccessor?.Destination is { } next) edges.Add(new GraphEdge(id, $"{sourceId}:block:{next.Ordinal}", "fall-through"));
            if (block.ConditionalSuccessor?.Destination is { } conditional) edges.Add(new GraphEdge(id, $"{sourceId}:block:{conditional.Ordinal}", "conditional"));
        }

        var semanticErrors = compilation.GetDiagnostics(ct).Count(d => d.Severity == DiagnosticSeverity.Error);
        return new StructureOutcome(nodes, edges, CyclomaticComplexity.ForCSharp(method), semanticErrors, null);
    }
}
