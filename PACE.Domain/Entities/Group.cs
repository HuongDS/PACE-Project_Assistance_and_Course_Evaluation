namespace PACE.Domain.Entities;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public string Name { get; set; } = null!;
    public string? TopicName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CourseTopicId { get; set; }
    public bool IsLocked { get; set; } = false;
    public bool IsApproved { get; set; } = true;

    public Class Class { get; set; } = null!;
    public CourseTopic? CourseTopic { get; set; }
    public ICollection<AnalysisRun> AnalysisRuns { get; set; } = new List<AnalysisRun>();
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<GroupInvitation> Invitations { get; set; } = new List<GroupInvitation>();
    public ICollection<GroupChangeRequest> ChangeRequests { get; set; } = new List<GroupChangeRequest>();
}

