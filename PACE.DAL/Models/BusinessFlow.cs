namespace PACE.DAL.Models;

public class BusinessFlow
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public Guid EntryMethodId { get; set; }
    public string Name { get; set; } = null!;
    public string? MermaidGraph { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
    public MethodSource EntryMethod { get; set; } = null!;
    public ICollection<DataFlowEdge> Edges { get; set; } = new List<DataFlowEdge>();
}
