using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class BusinessMethod
{
    public Guid BusinessId { get; set; }
    public Guid MethodSourceId { get; set; }

    public Business Business { get; set; } = null!;
    public MethodSource MethodSource { get; set; } = null!;
}
