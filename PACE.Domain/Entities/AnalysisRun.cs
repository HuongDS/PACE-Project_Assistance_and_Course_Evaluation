using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class AnalysisRun
{
    public Guid Id { get; set; }
    public string? RepoUrl { get; set; }
    public string? RepoName { get; set; }
    public string? AnalyzedCommitSha { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Template nghiệp vụ dùng cho lần phân tích gần nhất (hoặc admin đã chốt), dạng JSON mảng nghiệp vụ.</summary>
    public string? BusinessTemplateJson { get; set; }
    public BusinessTemplateSource? TemplateSource { get; set; }
    public DateTime? TemplateUpdatedAt { get; set; }

    public ICollection<Business> Businesses { get; set; } = new List<Business>();
    public ICollection<StudentSubmission> Submissions { get; set; } = new List<StudentSubmission>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<MethodSource> MethodSources { get; set; } = new List<MethodSource>();
}
