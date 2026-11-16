using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class GroupChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public GroupChangeRequestType Type { get; set; }
    public GroupChangeRequestStatus Status { get; set; } = GroupChangeRequestStatus.Pending;
    public string Title { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string PayloadJson { get; set; } = "{}";
    public bool FailStudentIfRemoved { get; set; } = false;

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public Group Group { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
}
