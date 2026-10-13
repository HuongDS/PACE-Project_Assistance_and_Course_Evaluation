using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class AnalysisRun
{
    public Guid Id { get; set; }
    public string? RepoUrl { get; set; }
    public string? RepoName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Business> Businesses { get; set; } = new List<Business>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<MethodSource> MethodSources { get; set; } = new List<MethodSource>();
}
