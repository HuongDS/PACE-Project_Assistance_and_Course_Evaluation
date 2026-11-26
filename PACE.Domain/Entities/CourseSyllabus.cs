namespace PACE.Domain.Entities;

/// <summary>Syllabus đang áp dụng của một môn: cột điểm và lịch buổi học, dùng để đề xuất mốc cho từng lớp.</summary>
public class CourseSyllabus
{
    public Guid CourseId { get; set; }
    /// <summary>Mã syllabus ở nơi lấy về, ví dụ mã trên FLM.</summary>
    public string? SourceId { get; set; }
    public string Name { get; set; } = null!;
    public string ContentJson { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Course Course { get; set; } = null!;
}
