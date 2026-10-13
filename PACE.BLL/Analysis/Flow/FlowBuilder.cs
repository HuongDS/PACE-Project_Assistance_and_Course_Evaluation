using PACE.BLL.Analysis.Models;
using PACE.DAL.Models;

namespace PACE.BLL.Analysis.Flow;

public sealed record FlowStep(int Order, ParsedMethod From, ParsedMethod To, FlowEdgeType EdgeType, string? Condition, string? DataLabel);

public sealed class FlowGraph
{
    public required ParsedMethod Entry { get; init; }
    public List<FlowStep> Steps { get; } = [];
    public HashSet<ParsedMethod> Methods { get; } = [];
    public bool Truncated { get; set; }
}

public sealed class FlowBuilder(CallResolver resolver, int maxDepth = 12, int maxSteps = 250)
{
    private static readonly HashSet<string> VoidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "void", "Task", "ValueTask", "Promise<void>", "Mono<Void>", "Promise<any>", "any", "unknown", "never", "IActionResult", "Task<IActionResult>", "ActionResult"
    };

    public FlowGraph Build(ParsedMethod entry)
    {
        var graph = new FlowGraph { Entry = entry };
        graph.Methods.Add(entry);
        var expanded = new HashSet<Guid> { entry.Id };
        var seen = new HashSet<(Guid, Guid, string?)>();
        Visit(entry, 0, graph, expanded, seen, new HashSet<Guid> { entry.Id });
        return graph;
    }

    private void Visit(ParsedMethod method, int depth, FlowGraph graph, HashSet<Guid> expanded, HashSet<(Guid, Guid, string?)> seen, HashSet<Guid> path)
    {
        foreach (var call in method.Calls)
        {
            foreach (var target in resolver.Resolve(call, method))
            {
                if (graph.Steps.Count >= maxSteps)
                {
                    graph.Truncated = true;
                    return;
                }

                if (!seen.Add((method.Id, target.Id, call.Condition))) continue;

                graph.Methods.Add(target);
                AddStep(graph, method, target, FlowEdgeType.Call, call.Condition, InputLabel(target));

                if (!path.Contains(target.Id) && depth + 1 < maxDepth && expanded.Add(target.Id))
                {
                    path.Add(target.Id);
                    Visit(target, depth + 1, graph, expanded, seen, path);
                    path.Remove(target.Id);
                }
                else if (depth + 1 >= maxDepth)
                {
                    graph.Truncated = true;
                }

                var output = OutputLabel(target);
                if (output is not null && graph.Steps.Count < maxSteps)
                    AddStep(graph, target, method, FlowEdgeType.Return, null, output);
            }
        }
    }

    private static void AddStep(FlowGraph graph, ParsedMethod from, ParsedMethod to, FlowEdgeType type, string? condition, string? label)
        => graph.Steps.Add(new FlowStep(graph.Steps.Count + 1, from, to, type, condition, label));

    public static string InputLabel(ParsedMethod target)
    {
        if (target.Parameters.Count == 0) return "Gọi hàm";
        var text = string.Join(", ", target.Parameters.Select(p => p.ToString()));
        return Shorten($"Truyền: {text}", 90);
    }

    public static string? OutputLabel(ParsedMethod target)
    {
        var type = target.ReturnType?.Trim();
        if (!string.IsNullOrEmpty(type))
        {
            var unwrapped = Unwrap(type);
            if (VoidTypes.Contains(type) || VoidTypes.Contains(unwrapped)) return null;
            return Shorten($"Trả ra: {unwrapped}", 70);
        }

        return string.IsNullOrWhiteSpace(target.ReturnExpression) ? null : Shorten($"Trả ra: {target.ReturnExpression}", 70);
    }

    private static string Unwrap(string type)
    {
        foreach (var wrapper in new[] { "Task<", "ValueTask<", "Promise<", "Mono<", "Observable<", "CompletableFuture<", "ResponseEntity<", "ActionResult<" })
        {
            if (type.StartsWith(wrapper, StringComparison.Ordinal) && type.EndsWith('>'))
                return Unwrap(type[wrapper.Length..^1].Trim());
        }
        return type;
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 3)] + "...";
}
