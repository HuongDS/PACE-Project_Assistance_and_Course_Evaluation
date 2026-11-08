namespace PACE.Application.QuestionGeneration.Drawio;

public interface IDrawioSpecificationStorage
{
    Task SaveSpecificationAsync(Guid businessId, DrawioProcessDiagram diagram, CancellationToken ct = default);
    Task<DrawioProcessDiagram?> GetSpecificationAsync(Guid businessId, CancellationToken ct = default);
    Task<bool> HasSpecificationAsync(Guid businessId, CancellationToken ct = default);
    Task DeleteSpecificationAsync(Guid businessId, CancellationToken ct = default);
}

