using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PACE.Application.QuestionGeneration.Drawio;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Services;

public class DbDrawioSpecificationStorage(
    AppDbContext db,
    FileDrawioSpecificationStorage fallback) : IDrawioSpecificationStorage
{
    private const int TitleMaxLength = 255;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task SaveSpecificationAsync(Guid businessId, DrawioProcessDiagram diagram, CancellationToken ct = default)
    {
        if (!await db.Businesses.AnyAsync(b => b.Id == businessId, ct))
        {
            await fallback.SaveSpecificationAsync(businessId, diagram, ct);
            return;
        }

        var title = string.IsNullOrWhiteSpace(diagram.DiagramName) ? "Diagram" : diagram.DiagramName.Trim();
        if (title.Length > TitleMaxLength) title = title[..TitleMaxLength];
        diagram.BusinessId = businessId;

        var entity = await db.BusinessDiagrams.FirstOrDefaultAsync(d =>
            d.BusinessId == businessId && d.Format == DiagramFormat.Drawio && d.Title == title, ct);
        if (entity is null)
        {
            entity = new BusinessDiagram
            {
                BusinessId = businessId,
                Title = title,
                Format = DiagramFormat.Drawio
            };
            db.BusinessDiagrams.Add(entity);
        }

        entity.SpecificationJson = JsonSerializer.Serialize(diagram, JsonOpts);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<DrawioProcessDiagram?> GetSpecificationAsync(Guid businessId, CancellationToken ct = default)
    {
        var diagrams = await db.BusinessDiagrams.AsNoTracking()
            .Where(d => d.BusinessId == businessId
                && (d.SpecificationJson != null || (d.Description != null && d.Description != "")))
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);
        if (diagrams.Count == 0) return await fallback.GetSpecificationAsync(businessId, ct);

        var result = new DrawioProcessDiagram { BusinessId = businessId };
        DateTime? processedAt = null;
        var names = new List<string>();
        var text = new StringBuilder();

        foreach (var diagram in diagrams)
        {
            var parsed = Deserialize(diagram.SpecificationJson);
            var block = parsed?.NormalizedFlowText;
            if (string.IsNullOrWhiteSpace(block)) block = diagram.Description;
            if (string.IsNullOrWhiteSpace(block)) continue;

            names.Add(diagram.Title);
            if (diagrams.Count > 1) text.AppendLine($"### {diagram.Title}");
            text.AppendLine(block.Trim());
            text.AppendLine();

            if (parsed is null) continue;
            result.Nodes.AddRange(parsed.Nodes);
            result.Edges.AddRange(parsed.Edges);
            result.ExecutionPaths.AddRange(parsed.ExecutionPaths);
            if (processedAt is null || parsed.ProcessedAt > processedAt) processedAt = parsed.ProcessedAt;
        }

        if (names.Count == 0) return null;
        result.DiagramName = string.Join(" + ", names);
        result.NormalizedFlowText = text.ToString().Trim();
        if (processedAt is not null) result.ProcessedAt = processedAt.Value;
        return result;
    }

    public async Task<bool> HasSpecificationAsync(Guid businessId, CancellationToken ct = default)
    {
        var exists = await db.BusinessDiagrams.AnyAsync(d => d.BusinessId == businessId
            && (d.SpecificationJson != null || (d.Description != null && d.Description != "")), ct);
        return exists || await fallback.HasSpecificationAsync(businessId, ct);
    }

    public async Task DeleteSpecificationAsync(Guid businessId, CancellationToken ct = default)
    {
        await db.BusinessDiagrams
            .Where(d => d.BusinessId == businessId && d.Format == DiagramFormat.Drawio)
            .ExecuteDeleteAsync(ct);
        await fallback.DeleteSpecificationAsync(businessId, ct);
    }

    private static DrawioProcessDiagram? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<DrawioProcessDiagram>(json, JsonOpts); }
        catch (JsonException) { return null; }
    }
}
