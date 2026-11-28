namespace PACE.Domain.Entities;

/// <summary>
/// Bản nháp đề xuất đề tài của nhóm, các thành viên soạn cùng lúc (realtime). Mỗi nhóm một bản nháp;
/// khi gửi duyệt, nội dung được chép sang một GroupTopicSubmission mới và bản nháp bị xoá.
/// Nguồn sự thật là tài liệu Yjs (YDocState + các GroupTopicDraftUpdate sau YDocSeq);
/// Title, Summary, ContentJson, NoteToLecturer là bản chiếu do client ghi lại để server xem trước và xuất PDF mà không cần đọc Yjs.
/// </summary>
public class GroupTopicDraft
{
    public Guid GroupId { get; set; }
    public Guid? TemplateId { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    /// <summary>Cùng cấu trúc với GroupTopicSubmission.ContentJson.</summary>
    public string ContentJson { get; set; } = "{}";
    public string? NoteToLecturer { get; set; }
    /// <summary>Tăng mỗi lần lưu; dùng làm khoá chống ghi đè khi hai thành viên cùng soạn.</summary>
    public int Revision { get; set; }
    /// <summary>Trạng thái Yjs đã gộp (Y.encodeStateAsUpdate). Rỗng: chưa gộp lần nào.</summary>
    public byte[]? YDocState { get; set; }
    /// <summary>Id của GroupTopicDraftUpdate cuối cùng đã được gộp vào YDocState.</summary>
    public long YDocSeq { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public ProposalTemplate? Template { get; set; }
    public ICollection<GroupTopicDraftUpdate> Updates { get; set; } = new List<GroupTopicDraftUpdate>();
}
