namespace PACE.Domain.Entities;

public enum GroupTopicStatus
{
    Pending,
    Approved,
    ChangesRequested,
    Rejected
}

/// <summary>File proposal của một bản nộp đến từ đâu.</summary>
public enum TopicProposalSource
{
    /// <summary>Nhóm tự tải file lên (cách cũ).</summary>
    Uploaded,
    /// <summary>Hệ thống tạo PDF từ nội dung nhóm soạn theo khung.</summary>
    Generated
}

public class GroupTopicSubmission
{
    public Guid GroupId { get; set; }
    public int Version { get; set; }
    public string Title { get; set; } = null!;
    /// <summary>Mô tả dạng chữ thường của bản nộp cũ. Bản nộp theo khung dùng Summary và ContentJson.</summary>
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

    /// <summary>Tóm tắt một câu, hiện ở danh sách duyệt của giảng viên.</summary>
    public string? Summary { get; set; }
    /// <summary>Khung đã dùng để soạn. Rỗng: bản nộp cũ hoặc khung đã bị xoá.</summary>
    public Guid? TemplateId { get; set; }
    /// <summary>Phiên bản của khung tại lúc nộp, để biết nhóm soạn theo bộ mục nào.</summary>
    public int? TemplateVersion { get; set; }
    /// <summary>
    /// Nội dung theo từng mục (jsonb), tự chứa cả tiêu đề mục để vẫn đọc được khi khung đổi:
    /// {"sections":[{"key":"context","title":"Bối cảnh và bài toán","doc":{...Tiptap JSON...}}],"team":[{"studentCode":"SE180123","task":"..."}]}
    /// </summary>
    public string? ContentJson { get; set; }
    /// <summary>HTML đã làm sạch ở server, dùng để xuất PDF và hiển thị cho giảng viên.</summary>
    public string? ContentHtml { get; set; }
    public TopicProposalSource ProposalSource { get; set; } = TopicProposalSource.Uploaded;
    /// <summary>Lời nhắn của nhóm gửi giảng viên kèm bản nộp.</summary>
    public string? NoteToLecturer { get; set; }

    public Group Group { get; set; } = null!;
    public ProposalTemplate? Template { get; set; }
    public ICollection<GroupTopicAttachment> Attachments { get; set; } = new List<GroupTopicAttachment>();
}
