namespace PACE.Domain.Enums;

[Flags]
public enum FlowTruncationReason
{
    None = 0,
    DepthLimit = 1,
    StepLimit = 2
}
