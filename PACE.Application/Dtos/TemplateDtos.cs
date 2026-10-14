using System.ComponentModel.DataAnnotations;
using PACE.Application.Analysis.Template;

namespace PACE.Application.Dtos;

public enum TemplateSource
{
    /// <summary>Repo đã có sẵn template_business.json.</summary>
    Repository,
    /// <summary>Gemini gom endpoint thành nghiệp vụ.</summary>
    Ai,
    /// <summary>Gom mỗi controller thành một nghiệp vụ.</summary>
    Controller
}

public class GenerateTemplateRequest
{
    [Required, Url, MaxLength(500)]
    public string RepoUrl { get; set; } = null!;

    /// <summary>Dùng Gemini để gom nghiệp vụ; lỗi hoặc chưa cấu hình thì tự gom theo controller.</summary>
    public bool UseAi { get; set; } = true;

    /// <summary>Bỏ qua template_business.json có sẵn trong repo và tạo mới.</summary>
    public bool Regenerate { get; set; }
}

public class ExportTemplateRequest
{
    [Required]
    public List<BusinessTemplate> Businesses { get; set; } = [];
}

public record GeneratedTemplateDto(
    string RepoUrl,
    TemplateSource Source,
    string DetectedStacks,
    int BusinessCount,
    int ApiCount,
    IReadOnlyList<BusinessTemplate> Businesses,
    IReadOnlyList<string> Warnings);
