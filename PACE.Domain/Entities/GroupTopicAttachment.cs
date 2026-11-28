namespace PACE.Domain.Entities;

/// <summary>Tài liệu kèm theo (mockup, khảo sát, sơ đồ...) của một bản nộp đề tài. Không bắt buộc, tối đa 5 file.</summary>
public class GroupTopicAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public int Version { get; set; }
    public string FileName { get; set; } = null!;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public byte[] Content { get; set; } = [];
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public GroupTopicSubmission Submission { get; set; } = null!;
}
