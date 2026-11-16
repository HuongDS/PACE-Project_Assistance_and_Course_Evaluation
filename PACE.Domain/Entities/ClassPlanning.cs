namespace PACE.Domain.Entities;

public enum ClassScheduleItemKind
{
    Milestone,
    Deadline
}

public class ClassMilestone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public ClassScheduleItemKind Kind { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime DueAt { get; set; }
    public Class Class { get; set; } = null!;
}

public class ClassAssessmentRound
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MilestoneId { get; set; }
    public string Title { get; set; } = null!;
    public decimal Weight { get; set; }
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public string ConfigurationJson { get; set; } = null!;
    public ClassMilestone Milestone { get; set; } = null!;
}

public class CourseAnnouncement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Class Class { get; set; } = null!;
}

public class AssessmentRoundRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoundId { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = null!;
    public decimal Weight { get; set; }
    public DateTime OpensAt { get; set; }
    public DateTime ClosesAt { get; set; }
    public DateTime DueAt { get; set; }
    public string ConfigurationJson { get; set; } = null!;
    public Guid PublishedBy { get; set; }
    public DateTime PublishedAt { get; set; }
    public ClassAssessmentRound Round { get; set; } = null!;
}
