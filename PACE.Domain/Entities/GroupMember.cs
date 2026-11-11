namespace PACE.Domain.Entities;

public class GroupMember
{
    public Guid GroupId { get; set; }
    public string StudentCode { get; set; } = null!;
    public string Role { get; set; } = "Member";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public StudentProfile StudentProfile { get; set; } = null!;
}

