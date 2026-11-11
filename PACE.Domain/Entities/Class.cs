namespace PACE.Domain.Entities;

public class Class
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid? SemesterId { get; set; }
    public string? Name { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly ActualStartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public Course Course { get; set; } = null!;
    public Semester? Semester { get; set; }
    public ICollection<StudentClass> StudentClasses { get; set; } = new List<StudentClass>();
    public ICollection<Group> Groups { get; set; } = new List<Group>();
    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
    public ICollection<OralExam> OralExams { get; set; } = new List<OralExam>();
}

