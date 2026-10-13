using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class GeneratedQuestion
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string QuestionText { get; set; } = null!;
    public string? SuggestedAnswer { get; set; }
    public string? Difficulty { get; set; }
    public ContextType ContextType { get; set; } = ContextType.Raw;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
    public ICollection<StudentAnswer> Answers { get; set; } = new List<StudentAnswer>();
}
