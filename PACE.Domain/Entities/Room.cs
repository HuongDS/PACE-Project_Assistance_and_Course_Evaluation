namespace PACE.Domain.Entities;

public class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = null!;
    public string? Building { get; set; }

    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
}

