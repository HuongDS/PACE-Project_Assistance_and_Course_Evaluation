using TreeSitter;
using PACE.Domain.Enums;
using PACE.Infrastructure.Analysis.Parsing;

namespace PACE.Infrastructure.Analysis.Evidence;

/// <summary>AST (cây cú pháp có tên) và độ phức tạp McCabe của một hàm Java, JavaScript hoặc TypeScript đã lưu bằng tree-sitter.</summary>
public static class TreeSitterStructure
{
    public static bool Supports(ProgrammingLanguage language) =>
        language is ProgrammingLanguage.Java or ProgrammingLanguage.JavaScript or ProgrammingLanguage.TypeScript;

    public static StructureOutcome BuildAst(Guid sourceId, string code, ProgrammingLanguage language, string? filePath, int nodeBudget, CancellationToken ct)
    {
        var java = language == ProgrammingLanguage.Java;
        var grammar = java ? "Java"
            : language == ProgrammingLanguage.TypeScript
                ? (string.Equals(Path.GetExtension(filePath), ".tsx", StringComparison.OrdinalIgnoreCase) ? "TSX" : "TypeScript")
                : "JavaScript";

        using var treeLanguage = new Language(grammar);
        using var parser = new Parser(treeLanguage);

        // Mã đã lưu là một hàm đứng riêng. Java luôn cần lớp bao; JS/TS thử đứng riêng (function, arrow) rồi mới bọc vào lớp (method của class).
        var attempts = java
            ? new[] { (Text: "class __PaceFragment {\n" + code + "\n}", Wrapped: true) }
            : new[] { (Text: code, Wrapped: false), (Text: "class __PaceFragment {\n" + code + "\n}", Wrapped: true) };

        foreach (var (text, wrapped) in attempts)
        {
            ct.ThrowIfCancellationRequested();
            using var tree = parser.Parse(text);
            if (tree is null || tree.RootNode.HasError) continue;

            var roots = wrapped
                ? tree.RootNode.Descendants().FirstOrDefault(n => n.Type == "class_body")?.NamedChildren.ToList() ?? []
                : tree.RootNode.NamedChildren.ToList();
            if (roots.Count == 0) continue;

            var nodes = new List<GraphNode>();
            var edges = new List<GraphEdge>();
            var counter = 0;
            var stack = new Stack<(Node Node, string? ParentId)>();
            for (var i = roots.Count - 1; i >= 0; i--) stack.Push((roots[i], null));
            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var (node, parentId) = stack.Pop();
                if (nodes.Count >= nodeBudget) return StructureOutcome.Fail("AST exceeds 2000 node limit.");

                var id = $"{sourceId}:{counter++}";
                nodes.Add(new GraphNode(id, node.Type, "statement"));
                if (parentId is not null) edges.Add(new GraphEdge(parentId, id, "syntax-child"));

                var children = node.NamedChildren;
                for (var i = children.Count - 1; i >= 0; i--) stack.Push((children[i], id));
            }

            var complexity = CyclomaticComplexity.IsTruncated(code) ? (int?)null : CyclomaticComplexity.ForTreeSitter(roots, java);
            return new StructureOutcome(nodes, edges, complexity, 0, null);
        }

        return StructureOutcome.Fail($"Persisted {(java ? "Java" : "JavaScript/TypeScript")} fragment cannot be parsed; verified AST unavailable.");
    }
}
