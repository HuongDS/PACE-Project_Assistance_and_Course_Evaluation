namespace PACE.Domain.Entities;

/// <summary>Một lần nhóm nộp một sản phẩm: tệp hoặc liên kết. Nộp lại tạo bản mới, bản cũ vẫn giữ.</summary>
public class GroupSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    /// <summary>Mốc hoặc hạn nộp của lớp; null là tài liệu không gắn mốc.</summary>
    public Guid? MilestoneId { get; set; }
    public string Deliverable { get; set; } = null!;
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    /// <summary>Nội dung tệp khi lưu trong database; null nếu tệp nằm ở kho ngoài.</summary>
    public byte[]? Content { get; set; }
    /// <summary>Khóa của tệp ở kho ngoài (Cloudinary); null nếu tệp nằm trong database.</summary>
    public string? StorageKey { get; set; }
    public string? Url { get; set; }
    public string? Note { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public ClassMilestone? Milestone { get; set; }
}

public enum SubmissionReviewStatus
{
    Accepted,
    NeedsRevision
}

/// <summary>Đánh giá của giảng viên cho bài một nhóm nộp ở một mốc. Điểm là điểm sản phẩm chung của nhóm.</summary>
public class GroupSubmissionReview
{
    public Guid GroupId { get; set; }
    public Guid MilestoneId { get; set; }
    public SubmissionReviewStatus Status { get; set; }
    public decimal? Score { get; set; }
    public string? Feedback { get; set; }
    public Guid ReviewedBy { get; set; }
    public DateTime ReviewedAt { get; set; }

    public Group Group { get; set; } = null!;
    public ClassMilestone Milestone { get; set; } = null!;
}
