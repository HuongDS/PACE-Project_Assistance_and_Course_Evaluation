namespace PACE.Domain.Entities;

public class OralExam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Class Class { get; set; } = null!;
    public ICollection<OralQuestion> Questions { get; set; } = new List<OralQuestion>();
    public ICollection<StudentOralSession> StudentOralSessions { get; set; } = new List<StudentOralSession>();
}

