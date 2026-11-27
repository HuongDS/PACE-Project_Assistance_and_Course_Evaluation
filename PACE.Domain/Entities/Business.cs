using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class Business
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public string BusinessName { get; set; } = null!;
    public string? Description { get; set; }
    public int OrderIndex { get; set; }
    public string? OwnerStudentCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AnalysisRun AnalysisRun { get; set; } = null!;
    public StudentProfile? Owner { get; set; }
    public ICollection<BusinessApi> Apis { get; set; } = new List<BusinessApi>();
    public ICollection<BusinessDiagram> Diagrams { get; set; } = new List<BusinessDiagram>();
    public ICollection<GeneratedQuestion> Questions { get; set; } = new List<GeneratedQuestion>();
    public ICollection<BusinessMethod> BusinessMethods { get; set; } = new List<BusinessMethod>();
    public ICollection<BusinessFlow> Flows { get; set; } = new List<BusinessFlow>();
}
