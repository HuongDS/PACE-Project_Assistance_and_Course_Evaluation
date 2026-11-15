namespace PACE.Infrastructure.Analysis.Evidence;

public sealed record GraphNode(string Id, string Label, string Type);

public sealed record GraphEdge(string Source, string Target, string Relation);

/// <summary>
/// Kết quả dựng AST/CFG cho một hàm đã lưu. <see cref="Error"/> khác null nghĩa là không dựng được
/// (endpoint trả 422); <see cref="Complexity"/> null nghĩa là không tính được độ phức tạp một cách đáng tin.
/// </summary>
public sealed record StructureOutcome(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    int? Complexity,
    int SemanticErrors,
    string? Error)
{
    public static StructureOutcome Fail(string error) => new([], [], null, 0, error);
}
