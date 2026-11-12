namespace PACE.Domain.Entities;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public string Name { get; set; } = null!;
    public string? TopicName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Class Class { get; set; } = null!;
    public ICollection<AnalysisRun> AnalysisRuns { get; set; } = new List<AnalysisRun>();
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}

