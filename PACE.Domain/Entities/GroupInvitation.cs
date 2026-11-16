using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class GroupInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string InvitedStudentCode { get; set; } = null!;
    public Guid InvitedByUserId { get; set; }
    public GroupInvitationStatus Status { get; set; } = GroupInvitationStatus.Pending;
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }

    public Group Group { get; set; } = null!;
    public StudentProfile InvitedStudent { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
}
