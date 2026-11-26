using System.Text.Json;
using PACE.Application.Analysis.Models;
using PACE.Application.Common.ReadModels;

namespace PACE.Application.QuestionGeneration;

public enum GroundedQuestionKind { BranchTrue, BranchFalse, Definition }

public sealed record GroundedQuestion(GroundedQuestionKind Kind, CallSite Anchor, string? Variable,
    IReadOnlyList<CallSite> Targets, string FilePath, string Question, string Answer);

public static class GroundedQuestionGenerator
{
    public const string Version = "pace-cfg-templates-v1";

    public static IReadOnlyList<GroundedQuestion> Generate(IEnumerable<MethodCode> methods, int count) =>
        methods.OrderBy(m => m.FilePath, StringComparer.Ordinal)
            .ThenBy(m => m.ClassName, StringComparer.Ordinal)
            .ThenBy(m => m.MethodName, StringComparer.Ordinal)
            .ThenBy(m => m.SourceCode, StringComparer.Ordinal)
            .SelectMany(GenerateForMethod)
            .DistinctBy(q => (q.FilePath, q.Anchor, q.Kind, q.Variable))
            .Take(count)
            .ToArray();

    private static IEnumerable<GroundedQuestion> GenerateForMethod(MethodCode method)
    {
        if (string.IsNullOrWhiteSpace(method.FilePath) || ReadFlow(method.JavaFlowJson) is not
            { Status: MethodFlowStatus.Complete, Nodes: not null, Edges: not null, DefUses: not null } flow)
            yield break;

        var entries = flow.Nodes.Where(node => node.Kind == MethodFlowNodeKind.Entry).Take(2).ToArray();
        if (entries.Length != 1 || flow.Nodes.Select(node => node.Id).Distinct().Count() != flow.Nodes.Count)
            yield break;

        var entry = entries[0];
        var sourceLines = method.SourceCode.Split('\n');
        var nodes = flow.Nodes.ToDictionary(node => node.Id);
        var edges = flow.Edges.ToLookup(edge => edge.From);
        var reachable = new HashSet<int> { entry.Id };
        var pending = new Queue<int>();
        pending.Enqueue(entry.Id);
        while (pending.TryDequeue(out var id))
        {
            foreach (var edge in edges[id])
                if (nodes.ContainsKey(edge.To) && reachable.Add(edge.To))
                    pending.Enqueue(edge.To);
        }

        foreach (var condition in flow.Nodes.Where(n => n.Kind == MethodFlowNodeKind.Condition && reachable.Contains(n.Id))
                     .OrderBy(n => n.Site.Line).ThenBy(n => n.Site.Column))
        {
            if (!EvidenceAt(condition.Site)) continue;
            foreach (var kind in new[] { MethodFlowEdgeKind.True, MethodFlowEdgeKind.False })
            {
                var targets = edges[condition.Id].Where(edge => edge.Kind == kind)
                    .Select(edge => nodes.GetValueOrDefault(edge.To)).Distinct().ToArray();
                if (targets.Length != 1 || targets[0] is not { Kind: not MethodFlowNodeKind.Exit } target ||
                    !EvidenceAt(target.Site))
                    continue;

                var isTrue = kind == MethodFlowEdgeKind.True;
                var outcome = isTrue ? "đúng" : "sai";
                var answer = $"Theo CFG, nhánh {outcome} dẫn tới {Reference(target.Site)}.";
                var question = $"Trong {method.ClassName}.{method.MethodName}, khi điều kiện tại " +
                               $"{Reference(condition.Site)} cho kết quả {outcome}, CFG đi tiếp tới vị trí nào?";
                yield return new GroundedQuestion(isTrue ? GroundedQuestionKind.BranchTrue : GroundedQuestionKind.BranchFalse,
                    condition.Site, null, [target.Site], method.FilePath, question, answer);
            }
        }

        foreach (var use in flow.DefUses.GroupBy(fact => (fact.Use, fact.Variable))
                     .OrderBy(group => group.Key.Use.Line).ThenBy(group => group.Key.Use.Column)
                     .ThenBy(group => group.Key.Variable, StringComparer.Ordinal))
        {
            var (site, variable) = use.Key;
            var definitions = use.Select(fact => fact.Definition).Distinct()
                .OrderBy(definition => definition.Line).ThenBy(definition => definition.Column).ToArray();
            if (string.IsNullOrEmpty(variable) || !EvidenceAt(site, variable) ||
                definitions.Any(definition => !EvidenceAt(definition, variable)))
                continue;

            var answer = $"Theo phân tích tĩnh, các vị trí định nghĩa có thể tới chỗ đọc này gồm {string.Join(", ", definitions.Select(Reference))}.";
            var question = $"Trong {method.ClassName}.{method.MethodName}, những vị trí định nghĩa nào có thể " +
                           $"đưa giá trị của biến `{variable}` tới chỗ đọc tại {Reference(site)}?";
            yield return new GroundedQuestion(GroundedQuestionKind.Definition, site, variable, definitions,
                method.FilePath, question, answer);
        }

        string Reference(CallSite site) => $"{method.FilePath}:{site.Line}:{site.Column}";

        bool EvidenceAt(CallSite site, string? identifier = null)
        {
            var line = site.Line - entry.Site.Line;
            if (line < 0 || line >= sourceLines.Length) return false;
            var startColumn = line == 0 ? entry.Site.Column : 1;
            var offset = site.Column - startColumn;
            var content = sourceLines[line].AsSpan().TrimEnd();
            return offset >= 0 && offset < content.Length && !char.IsWhiteSpace(content[offset]) &&
                   (identifier is null || content[offset..].StartsWith(identifier, StringComparison.Ordinal));
        }
    }

    private static MethodFlowAnalysis? ReadFlow(string? json)
    {
        try
        {
            return MethodFlowAnalysis.Deserialize(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
