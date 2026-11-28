namespace PACE.Domain.Entities;

/// <summary>
/// Một thay đổi Yjs (Y.encodeUpdate) do một thành viên gửi lên khi soạn bản nháp đề xuất.
/// Server chỉ lưu và phát lại cho cả nhóm, không cần hiểu nội dung. Khi log dài, một client gộp
/// toàn bộ thành YDocState của bản nháp và các dòng đã gộp bị xoá.
/// </summary>
public class GroupTopicDraftUpdate
{
    /// <summary>Tăng dần, dùng để biết client còn thiếu thay đổi nào khi kết nối lại.</summary>
    public long Id { get; set; }
    public Guid GroupId { get; set; }
    public byte[] Update { get; set; } = [];
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public GroupTopicDraft Draft { get; set; } = null!;
}
