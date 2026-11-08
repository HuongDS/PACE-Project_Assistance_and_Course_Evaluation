using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PACE.Application.QuestionGeneration.Drawio;

public class FileDrawioSpecificationStorage(
    IOptions<QuestionGenerationOptions> options,
    ILogger<FileDrawioSpecificationStorage> logger) : IDrawioSpecificationStorage
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _storageDir = Path.IsPathRooted(options.Value.SpecificationStoragePath)
        ? options.Value.SpecificationStoragePath
        : Path.Combine(AppContext.BaseDirectory, options.Value.SpecificationStoragePath);

    private string GetFilePath(Guid businessId)
    {
        string dir = _storageDir;
        try
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Fallback to writable OS temp directory if configured directory has no write permission (e.g. non-root container)
            dir = Path.Combine(Path.GetTempPath(), "pace_specifications");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        return Path.Combine(dir, $"{businessId}.json");
    }

    public async Task SaveSpecificationAsync(Guid businessId, DrawioProcessDiagram diagram, CancellationToken ct = default)
    {
        var filePath = GetFilePath(businessId);
        diagram.BusinessId = businessId;
        var json = JsonSerializer.Serialize(diagram, JsonOpts);
        await File.WriteAllTextAsync(filePath, json, ct);
        logger.LogInformation("Đã lưu đặc tả sơ đồ Draw.io cho Business {BusinessId} vào file {FilePath}", businessId, filePath);
    }

    public async Task<DrawioProcessDiagram?> GetSpecificationAsync(Guid businessId, CancellationToken ct = default)
    {
        var filePath = GetFilePath(businessId);
        if (!File.Exists(filePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            return JsonSerializer.Deserialize<DrawioProcessDiagram>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đọc được file đặc tả Draw.io của Business {BusinessId}", businessId);
            return null;
        }
    }

    public Task<bool> HasSpecificationAsync(Guid businessId, CancellationToken ct = default)
    {
        var filePath = GetFilePath(businessId);
        return Task.FromResult(File.Exists(filePath));
    }

    public Task DeleteSpecificationAsync(Guid businessId, CancellationToken ct = default)
    {
        var filePath = GetFilePath(businessId);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            logger.LogInformation("Đã xoá đặc tả sơ đồ Draw.io của Business {BusinessId}", businessId);
        }
        return Task.CompletedTask;
    }
}

