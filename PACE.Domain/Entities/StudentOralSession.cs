namespace PACE.Domain.Entities;

public class StudentOralSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OralExamId { get; set; }
    public string StudentCode { get; set; } = null!;
    public string? ExaminerCode { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? AudioRecordUrl { get; set; }
    public string? ScreenRecordUrl { get; set; }
    public string? AiEvaluationSummary { get; set; }
    public string? FinalFeedback { get; set; }

    public OralExam OralExam { get; set; } = null!;
    public StudentProfile StudentProfile { get; set; } = null!;
    public LecturerProfile? Examiner { get; set; }
    public ICollection<StudentOralAnswer> Answers { get; set; } = new List<StudentOralAnswer>();
}

