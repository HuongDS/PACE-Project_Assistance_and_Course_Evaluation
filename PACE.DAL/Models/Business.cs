namespace PACE.DAL.Models;

public class Business
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public string BusinessName { get; set; } = null!;

    public AnalysisRun AnalysisRun { get; set; } = null!;
    public ICollection<GeneratedQuestion> Questions { get; set; } = new List<GeneratedQuestion>();
    public ICollection<BusinessMethod> BusinessMethods { get; set; } = new List<BusinessMethod>();
    public ICollection<BusinessFlow> Flows { get; set; } = new List<BusinessFlow>();
}
