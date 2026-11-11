namespace PACE.Domain.Entities;

public class StudentProfile
{
    public string StudentCode { get; set; } = null!;
    public Guid UserId { get; set; }
    public int EnrollmentYear { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<StudentClass> StudentClasses { get; set; } = new List<StudentClass>();
    public ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<StudentOralSession> OralSessions { get; set; } = new List<StudentOralSession>();
}

