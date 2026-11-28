namespace PACE.Domain.Entities;

/// <summary>
/// Khung đề xuất đề tài: danh sách mục nhóm phải viết. Phạm vi theo thứ tự ưu tiên:
/// gắn lớp (ClassId) &gt; gắn môn (CourseId) &gt; dùng chung (cả hai rỗng, khung mặc định của hệ thống).
/// </summary>
public class ProposalTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CourseId { get; set; }
    public Guid? ClassId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    /// <summary>
    /// Danh sách mục (jsonb), theo đúng thứ tự hiển thị:
    /// [{"key":"context","title":"Bối cảnh và bài toán","hint":"...","minWords":60,"required":true,"type":"richtext"}].
    /// type là "richtext" hoặc "team" (bảng thành viên và phân công).
    /// </summary>
    public string SectionsJson { get; set; } = "[]";
    /// <summary>Tăng mỗi lần sửa danh sách mục; bản nộp lưu lại số này.</summary>
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    /// <summary>Thứ tự hiện trong danh sách chọn khung.</summary>
    public int SortOrder { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Course? Course { get; set; }
    public Class? Class { get; set; }
}
