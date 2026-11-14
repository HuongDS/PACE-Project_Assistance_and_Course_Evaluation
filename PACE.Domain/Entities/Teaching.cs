namespace PACE.Domain.Entities;

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Credits { get; set; } = 3;
    public string Semester { get; set; } = "";
    public Guid LecturerId { get; set; }
    public User Lecturer { get; set; } = null!;
    public ICollection<CourseProject> Projects { get; set; } = new List<CourseProject>();
    public ICollection<Class> Classes { get; set; } = new List<Class>();
}
public class CourseProject
{
    public Guid AnalysisRunId { get; set; }
    public AnalysisRun AnalysisRun { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public bool? IsApproved { get; set; }
    public string? Notes { get; set; }
    public DateTime? DecidedAt { get; set; }
}
public class AssessmentSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public CourseProject Project { get; set; } = null!;
    public Guid? OralExamId { get; set; }
    public OralExam? OralExam { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "CREATED";
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public string RubricJson { get; set; } = "[]";
    public Guid Version { get; set; } = Guid.NewGuid();
    public ICollection<SessionStudent> Students { get; set; } = new List<SessionStudent>();
}
public class SessionStudent
{
    public Guid SessionId { get; set; }
    public AssessmentSession Session { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public string? EvaluationJson { get; set; }
    public Guid? EvaluatorId { get; set; }
    public DateTime? EvaluatedAt { get; set; }
}
public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = "";
    public Guid ResourceId { get; set; }
    public string EventType { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
