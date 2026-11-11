namespace PACE.Domain.Entities;

public class ClassSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Guid? ScheduleId { get; set; }
    public DateOnly LearnDate { get; set; }
    public Guid TimeSlotId { get; set; }
    public Guid RoomId { get; set; }
    public string LecturerCode { get; set; } = null!;
    public bool IsCancelled { get; set; } = false;
    public string? Note { get; set; }

    public Class Class { get; set; } = null!;
    public ClassSchedule? Schedule { get; set; }
    public TimeSlot TimeSlot { get; set; } = null!;
    public Room Room { get; set; } = null!;
    public LecturerProfile LecturerProfile { get; set; } = null!;
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}

