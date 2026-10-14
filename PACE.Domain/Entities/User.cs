using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string? StudentCode { get; set; }
    public UserRole Role { get; set; } = UserRole.Student;
    public Guid? AnalysisRunId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AnalysisRun? AnalysisRun { get; set; }
    public ICollection<StudentSubmission> Submissions { get; set; } = new List<StudentSubmission>();
    public ICollection<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
}
