using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class DataFlowEdge
{
    public Guid Id { get; set; }
    public Guid FlowId { get; set; }
    public int StepOrder { get; set; }
    public Guid FromMethodId { get; set; }
    public Guid ToMethodId { get; set; }
    public FlowEdgeType EdgeType { get; set; } = FlowEdgeType.Call;
    public string? ConditionContext { get; set; }
    public string? DataLabel { get; set; }

    public BusinessFlow Flow { get; set; } = null!;
    public MethodSource FromMethod { get; set; } = null!;
    public MethodSource ToMethod { get; set; } = null!;
}
