namespace PACE.Domain.Entities;

public class StudentOralAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public string? StudentAnswer { get; set; }
    public string? Feedback { get; set; }

    public StudentOralSession Session { get; set; } = null!;
    public OralQuestion Question { get; set; } = null!;
}

