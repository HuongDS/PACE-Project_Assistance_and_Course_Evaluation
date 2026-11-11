namespace PACE.Domain.Entities;

public class LecturerProfile
{
    public string LecturerCode { get; set; } = null!;
    public Guid UserId { get; set; }
    public string AcademicTitle { get; set; } = "Lecturer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<ClassSchedule> ClassSchedules { get; set; } = new List<ClassSchedule>();
    public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
    public ICollection<StudentOralSession> ExaminedOralSessions { get; set; } = new List<StudentOralSession>();
}

