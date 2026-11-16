namespace PACE.Domain.Entities;

using PACE.Domain.Enums;

public class Milestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MilestoneType Type { get; set; } = MilestoneType.Other;
    public DateTime? StartDate { get; set; }
    public DateTime Deadline { get; set; }
    public bool IsLocked { get; set; } = false;
    public int OrderIndex { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
