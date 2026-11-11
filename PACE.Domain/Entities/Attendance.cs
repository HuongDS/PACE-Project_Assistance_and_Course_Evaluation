namespace PACE.Domain.Entities;

public class Attendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string StudentCode { get; set; } = null!;
    public int Status { get; set; }
    public DateTime? RecordedAt { get; set; }
    public string? Comments { get; set; }

    public ClassSession Session { get; set; } = null!;
    public StudentProfile StudentProfile { get; set; } = null!;
}

