using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class MethodSource
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public string? FilePath { get; set; }
    public string ClassName { get; set; } = null!;
    public string MethodName { get; set; } = null!;
    public string? Parameters { get; set; }
    public string SourceCode { get; set; } = null!;
    public ProgrammingLanguage Language { get; set; } = ProgrammingLanguage.Other;
    public NodeType NodeType { get; set; } = NodeType.Activity;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AnalysisRun AnalysisRun { get; set; } = null!;
    public ICollection<BusinessMethod> BusinessMethods { get; set; } = new List<BusinessMethod>();
    public ICollection<DataFlowEdge> OutgoingEdges { get; set; } = new List<DataFlowEdge>();
    public ICollection<DataFlowEdge> IncomingEdges { get; set; } = new List<DataFlowEdge>();
}
