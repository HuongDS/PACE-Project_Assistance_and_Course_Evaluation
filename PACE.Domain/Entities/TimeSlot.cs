namespace PACE.Domain.Entities;

public class TimeSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PeriodNumber { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
}

