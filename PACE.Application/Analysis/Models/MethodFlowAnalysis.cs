using System.Text.Json;
using System.Text.Json.Serialization;

namespace PACE.Application.Analysis.Models;

[JsonConverter(typeof(JsonStringEnumConverter<MethodFlowStatus>))]
public enum MethodFlowStatus { Complete, Unsupported, LimitReached }

[JsonConverter(typeof(JsonStringEnumConverter<MethodFlowNodeKind>))]
public enum MethodFlowNodeKind { Entry, Exit, Statement, Condition, Return, Throw, Break, Continue }

[JsonConverter(typeof(JsonStringEnumConverter<MethodFlowEdgeKind>))]
public enum MethodFlowEdgeKind { Next, True, False, Return, Throw, Break, Continue }

public sealed record MethodFlowNode(int Id, MethodFlowNodeKind Kind, CallSite Site, string? Condition = null);
public sealed record MethodFlowEdge(int From, int To, MethodFlowEdgeKind Kind);
public sealed record MethodDefUse(string Variable, CallSite Definition, CallSite Use);

public sealed record MethodFlowAnalysis(MethodFlowStatus Status, string? Reason, IReadOnlyList<MethodFlowNode> Nodes,
    IReadOnlyList<MethodFlowEdge> Edges, IReadOnlyList<MethodDefUse> DefUses)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    public static MethodFlowAnalysis? Deserialize(string? json) => json is null
        ? null : JsonSerializer.Deserialize<MethodFlowAnalysis>(json, Json);
}
