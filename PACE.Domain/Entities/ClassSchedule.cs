namespace PACE.Domain.Entities;

public class ClassSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public string LecturerCode { get; set; } = null!;
    public Guid RoomId { get; set; }
    public Guid TimeSlotId { get; set; }
    public int DayOfWeek { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }

    public Class Class { get; set; } = null!;
    public LecturerProfile LecturerProfile { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public TimeSlot TimeSlot { get; set; } = null!;
    public ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
}

