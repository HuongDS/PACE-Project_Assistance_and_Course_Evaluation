namespace PACE.Domain.Entities;

/// <summary>Một phiên bản syllabus của môn, áp dụng từ một kỳ cho tới khi có phiên bản mới hơn.</summary>
public class CourseSyllabus
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    /// <summary>Kỳ đầu tiên áp dụng phiên bản này.</summary>
    public Guid SemesterId { get; set; }
    /// <summary>Mã syllabus ở nơi lấy về, ví dụ mã trên FLM.</summary>
    public string? SourceId { get; set; }
    public string Name { get; set; } = null!;
    public string ContentJson { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Course Course { get; set; } = null!;
    public Semester Semester { get; set; } = null!;
}
