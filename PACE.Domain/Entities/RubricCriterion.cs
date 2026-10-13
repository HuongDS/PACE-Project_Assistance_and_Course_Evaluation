using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class RubricCriterion
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Statement { get; set; } = null!;
    public int DisplayOrder { get; set; }
}
