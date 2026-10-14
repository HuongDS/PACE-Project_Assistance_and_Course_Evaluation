using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class StudentAnswer
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? SubmissionId { get; set; }
    public Guid QuestionId { get; set; }
    public string? AnswerText { get; set; }

    public string? FeedbackFormat { get; set; }
    public string? AiFeedback { get; set; }

    public string? StudentReply { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StudentSubmission? Submission { get; set; }
    public User User { get; set; } = null!;
    public GeneratedQuestion Question { get; set; } = null!;
    public ICollection<RubricScore> RubricScores { get; set; } = new List<RubricScore>();
}
