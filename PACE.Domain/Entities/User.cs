using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? StudentCode { get; set; }
    public UserRole Role { get; set; } = UserRole.Student;
    public Guid? AnalysisRunId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StudentProfile? StudentProfile { get; set; }
    public LecturerProfile? LecturerProfile { get; set; }

    public AnalysisRun? AnalysisRun { get; set; }
    public ICollection<StudentSubmission> Submissions { get; set; } = new List<StudentSubmission>();
    public ICollection<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
}
