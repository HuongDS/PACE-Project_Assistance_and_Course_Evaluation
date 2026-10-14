using System.ComponentModel.DataAnnotations;
using PACE.Application.Analysis.Template;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos;

public class GenerateTemplateRequest
{
    [Required, Url, MaxLength(500)]
    public string RepoUrl { get; set; } = null!;

    /// <summary>Dùng Gemini để gom nghiệp vụ; lỗi hoặc chưa cấu hình thì tự gom theo controller.</summary>
    public bool UseAi { get; set; } = true;

    /// <summary>Bỏ qua template đã chốt trong DB và template_business.json có sẵn trong repo, tạo mới.</summary>
    public bool Regenerate { get; set; }
}

public class BusinessTemplateRequest
{
    [Required]
    public List<BusinessTemplate> Businesses { get; set; } = [];
}

public record GeneratedTemplateDto(
    string RepoUrl,
    BusinessTemplateSource Source,
    string DetectedStacks,
    int BusinessCount,
    int ApiCount,
    IReadOnlyList<BusinessTemplate> Businesses,
    IReadOnlyList<string> Warnings);

public record AnalysisRunTemplateDto(
    Guid AnalysisRunId,
    BusinessTemplateSource Source,
    DateTime? UpdatedAt,
    int BusinessCount,
    int ApiCount,
    IReadOnlyList<BusinessTemplate> Businesses);
