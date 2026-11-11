namespace PACE.Domain.Entities;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public string Name { get; set; } = null!;
    public string? TopicName { get; set; }
    public Guid? AnalysisRunId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Class Class { get; set; } = null!;
    public AnalysisRun? AnalysisRun { get; set; }
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}

