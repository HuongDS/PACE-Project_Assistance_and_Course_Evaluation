namespace PACE.Domain.Entities;

public class StudentSubmission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AnalysisRunId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public AnalysisRun AnalysisRun { get; set; } = null!;
    public ICollection<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
}
