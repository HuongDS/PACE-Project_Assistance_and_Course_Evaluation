using System.Text;
using PACE.DAL.Models;

namespace PACE.BLL.Analysis.Flow;

public static class MermaidBuilder
{
    public static string Build(FlowGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");

        var nodeIds = new Dictionary<Guid, string>();
        var duplicated = graph.Steps.SelectMany(s => new[] { s.From, s.To }).Append(graph.Entry)
            .DistinctBy(m => m.Id)
            .GroupBy(m => m.Label)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();
        string NodeId(Models.ParsedMethod method)
        {
            if (nodeIds.TryGetValue(method.Id, out var id)) return id;
            id = $"M{nodeIds.Count}";
            nodeIds[method.Id] = id;
            var shape = method.IsDecision ? ("{{\"", "\"}}") : ("[\"", "\"]");
            var text = duplicated.Contains(method.Label)
                ? $"{method.Label}({string.Join(", ", method.Parameters.Select(p => p.Type is null ? p.Name : Parsing.ParserText.SimpleTypeName(p.Type)))})"
                : method.Label;
            sb.AppendLine($"    {id}{shape.Item1}{Escape(text)}{shape.Item2}");
            return id;
        }

        NodeId(graph.Entry);
        foreach (var method in graph.Steps.SelectMany(s => new[] { s.From, s.To }))
            NodeId(method);

        var decisionIds = new Dictionary<(Guid, string), string>();
        foreach (var step in graph.Steps)
        {
            var from = NodeId(step.From);
            var to = NodeId(step.To);
            var label = Escape($"{step.Order}. {step.DataLabel}");

            if (step.EdgeType == FlowEdgeType.Return)
            {
                sb.AppendLine($"    {from} -.->|\"{label}\"| {to}");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(step.Condition))
            {
                var key = (step.From.Id, step.Condition);
                if (!decisionIds.TryGetValue(key, out var decisionId))
                {
                    decisionId = $"D{decisionIds.Count}";
                    decisionIds[key] = decisionId;
                    sb.AppendLine($"    {decisionId}{{\"{Escape(step.Condition)}\"}}");
                    sb.AppendLine($"    {from} -.-> {decisionId}");
                }
                sb.AppendLine($"    {decisionId} -->|\"{label}\"| {to}");
                continue;
            }

            sb.AppendLine($"    {from} -->|\"{label}\"| {to}");
        }

        sb.AppendLine("    classDef entry fill:#e3f2fd,stroke:#1565c0,stroke-width:2px");
        sb.AppendLine($"    class {nodeIds[graph.Entry.Id]} entry");
        return sb.ToString().TrimEnd();
    }

    private static string Escape(string text)
        => text.Replace("\r", " ").Replace("\n", " ")
            .Replace("&", "#amp;")
            .Replace("\"", "#quot;")
            .Replace("<", "#lt;")
            .Replace(">", "#gt;")
            .Replace("|", "#124;")
            .Replace("`", "'");
}
