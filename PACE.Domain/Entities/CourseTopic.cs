namespace PACE.Domain.Entities;

public class CourseTopic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    /// <summary>Kỳ mà đề tài được mở. Rỗng: dùng chung mọi kỳ.</summary>
    public Guid? SemesterId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Requirements { get; set; }
    public int MaxGroups { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Course Course { get; set; } = null!;
    public Semester? Semester { get; set; }
    public ICollection<Group> Groups { get; set; } = new List<Group>();
}
