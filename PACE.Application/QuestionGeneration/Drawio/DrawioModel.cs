namespace PACE.Application.QuestionGeneration.Drawio;

public record DrawioNode(
    string Id,
    string Label,
    string? ShapeType = null,
    string? Swimlane = null);

public record DrawioEdge(
    string Id,
    string SourceId,
    string TargetId,
    string? Label = null);

public class DrawioProcessDiagram
{
    public Guid BusinessId { get; set; }
    public string DiagramName { get; set; } = string.Empty;
    public List<DrawioNode> Nodes { get; set; } = [];
    public List<DrawioEdge> Edges { get; set; } = [];
    public List<string> ExecutionPaths { get; set; } = [];
    public string NormalizedFlowText { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

public record DrawioUploadRequest
{
    /// <summary>Nội dung chuỗi XML thô từ file .xml của Draw.io (nếu gửi dạng JSON).</summary>
    public string? RawXml { get; set; }
    public string? DiagramName { get; set; }
}

public record DrawioProcessDiagramDto(
    Guid BusinessId,
    string DiagramName,
    int NodeCount,
    int EdgeCount,
    int PathCount,
    string NormalizedFlowText,
    DateTime ProcessedAt);

