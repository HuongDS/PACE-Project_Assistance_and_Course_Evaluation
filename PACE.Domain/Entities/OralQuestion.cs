namespace PACE.Domain.Entities;

public class OralQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OralExamId { get; set; }
    public Guid? GeneratedQuestionId { get; set; }
    public string Content { get; set; } = null!;
    public string? ReferenceContext { get; set; }
    public string? ExpectedKeywords { get; set; }

    public OralExam OralExam { get; set; } = null!;
    public GeneratedQuestion? GeneratedQuestion { get; set; }
    public ICollection<StudentOralAnswer> Answers { get; set; } = new List<StudentOralAnswer>();
}

