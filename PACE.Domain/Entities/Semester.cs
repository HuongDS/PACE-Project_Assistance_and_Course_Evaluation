namespace PACE.Domain.Entities;

public class Semester
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string AcademicYear { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly ActualStartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public ICollection<Class> Classes { get; set; } = new List<Class>();
}

