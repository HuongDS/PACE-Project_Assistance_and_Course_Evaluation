namespace PACE.Domain.Entities;

public class StudentClass
{
    public string StudentCode { get; set; } = null!;
    public Guid ClassId { get; set; }
    public int Status { get; set; } = 1;

    public StudentProfile StudentProfile { get; set; } = null!;
    public Class Class { get; set; } = null!;
}

