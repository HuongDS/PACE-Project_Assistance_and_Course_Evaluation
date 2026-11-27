namespace PACE.Domain.Entities;

public enum GroupTopicStatus
{
    Pending,
    Approved,
    ChangesRequested,
    Rejected
}

public class GroupTopicSubmission
{
    public Guid GroupId { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public GroupTopicStatus Status { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Notes { get; set; }
    public string? ProposalFileName { get; set; }
    public string? ProposalContentType { get; set; }
    public long? ProposalFileSize { get; set; }
    public byte[]? ProposalFile { get; set; }
    public Group Group { get; set; } = null!;
}
