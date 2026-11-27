using PACE.Application.Analysis.Models;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Analysis.Flow;

public sealed record FlowStep(int Order, ParsedMethod From, ParsedMethod To, FlowEdgeType EdgeType, string? Condition, string? DataLabel, CallSite? Site);

public sealed class FlowGraph
{
    public required ParsedMethod Entry { get; init; }
    public List<FlowStep> Steps { get; } = [];
    public HashSet<ParsedMethod> Methods { get; } = [];
    public FlowTruncationReason TruncationReason { get; internal set; }
    public bool Truncated => TruncationReason != FlowTruncationReason.None;
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
        var pending = new Queue<(ParsedMethod Method, int Depth)>();
        pending.Enqueue((entry, 0));

        while (pending.TryDequeue(out var item))
        {
            foreach (var (call, target) in ResolveOccurrences(item.Method))
            {
                if (item.Depth >= maxDepth)
                {
                    graph.TruncationReason |= FlowTruncationReason.DepthLimit;
                    break;
                }

                if (!TryAddStep(graph, item.Method, target, FlowEdgeType.Call, call.Condition, InputLabel(target), call.Site))
                    return graph;

                if (graph.Methods.Add(target)) pending.Enqueue((target, item.Depth + 1));

                if (OutputLabel(target) is { } output &&
                    !TryAddStep(graph, target, item.Method, FlowEdgeType.Return, null, output, call.Site))
                    return graph;
            }
        }
        return graph;
    }

    private IEnumerable<(ParsedCall Call, ParsedMethod Target)> ResolveOccurrences(ParsedMethod caller)
    {
        var seen = new HashSet<(CallSite? Site, ParsedCall? Unlocated, Guid Target)>();
        foreach (var call in caller.Calls)
        {
            foreach (var target in resolver.Resolve(call, caller))
            {
                if (seen.Add((call.Site, call.Site is null ? call : null, target.Id)))
                    yield return (call, target);
            }
        }
    }

    private bool TryAddStep(FlowGraph graph, ParsedMethod from, ParsedMethod to, FlowEdgeType edgeType,
        string? condition, string? label, CallSite? site)
    {
        if (graph.Steps.Count >= maxSteps)
        {
            graph.TruncationReason |= FlowTruncationReason.StepLimit;
            return false;
        }
        graph.Steps.Add(new FlowStep(graph.Steps.Count + 1, from, to, edgeType, condition, label, site));
        return true;
    }

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
