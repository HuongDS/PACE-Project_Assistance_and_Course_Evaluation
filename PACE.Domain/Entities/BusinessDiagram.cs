using PACE.Domain.Enums;

namespace PACE.Domain.Entities;

public class BusinessDiagram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BusinessId { get; set; }
    public string Title { get; set; } = null!;
    public DiagramFormat Format { get; set; } = DiagramFormat.Drawio;
    public string? SourceXml { get; set; }
    public string? SpecificationJson { get; set; }
    public string? PreviewUrl { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Business Business { get; set; } = null!;
}
