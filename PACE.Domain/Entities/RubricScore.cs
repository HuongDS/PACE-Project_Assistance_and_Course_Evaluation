using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class RubricScore
{
    public Guid Id { get; set; }
    public Guid StudentAnswerId { get; set; }
    public Guid CriterionId { get; set; }
    public int Score { get; set; }

    public StudentAnswer StudentAnswer { get; set; } = null!;
    public RubricCriterion Criterion { get; set; } = null!;
}
