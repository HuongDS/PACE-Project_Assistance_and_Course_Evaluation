using System.ComponentModel.DataAnnotations;
using PACE.Application.Analysis.Template;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos;

public class GenerateTemplateRequest
{
    [Required, MaxLength(500)]
    public string RepoUrl { get; set; } = null!;

    /// <summary>Dùng Gemini để gom nghiệp vụ; lỗi hoặc chưa cấu hình thì tự gom theo controller.</summary>
    public bool UseAi { get; set; } = true;

    /// <summary>Bỏ qua template đã chốt trong DB và template_business.json có sẵn trong repo, tạo mới.</summary>
    public bool Regenerate { get; set; }

    /// <summary>
    /// API mà AI bỏ sót khi gom nhóm: true thì tự gom theo controller (template đủ API, dùng khi xuất file);
    /// false thì để ngoài template, chưa thuộc nghiệp vụ nào, để sinh viên tự chọn nghiệp vụ trên trang Dự án.
    /// </summary>
    public bool GroupMissingByController { get; set; } = true;

    /// <summary>Số lượng API mỗi batch gửi lên Gemini (mặc định 12; dùng cho Thực nghiệm 4 - Ablation Study).</summary>
    [Range(1, 50)]
    public int? BatchSize { get; set; }

    /// <summary>Số lượng EvidenceNode tối đa mỗi API (mặc định 25; dùng cho Thực nghiệm 4 - Ablation Study).</summary>
    [Range(5, 100)]
    public int? MaxNodes { get; set; }
}

public class CompareTemplateRequest
{
    [Required, MaxLength(500)]
    public string RepoUrl { get; set; } = null!;

    /// <summary>Dùng Gemini để so sánh cả chế độ AI cũ vs AI Hybrid mới; nếu false thì so sánh gom theo Controller cũ vs gom theo Controller kèm phân tích tĩnh Hybrid.</summary>
    public bool UseAi { get; set; } = true;

    [Range(1, 50)]
    public int? BatchSize { get; set; }

    [Range(5, 100)]
    public int? MaxNodes { get; set; }
}

public record EndpointComparisonItemDto(
    int EndpointId,
    string Controller,
    string HttpMethod,
    string Function,
    string? Path,
    string LegacyBusinessName,
    string HybridBusinessName,
    bool BusinessGroupChanged,
    string? HybridSummary,
    int BehaviorClaimCount,
    int EvidenceNodeCount,
    ApiCodeAnchors CodeAnchors);

public record TemplateComparisonMetricsDto(
    long LegacyDurationMs,
    long HybridDurationMs,
    int LegacyInputTokens,
    int LegacyOutputTokens,
    int HybridInputTokens,
    int HybridOutputTokens,
    int LegacyBusinessCount,
    int HybridBusinessCount,
    int TotalEndpoints,
    int EndpointsWithDbReads,
    int EndpointsWithDbWrites,
    int EndpointsWithExternalSdks,
    int EndpointsWithChangedBusinessGroup);

public record CompareTemplateResultDto(
    string RepoUrl,
    string DetectedStacks,
    TemplateComparisonMetricsDto Metrics,
    GeneratedTemplateDto Legacy,
    GeneratedTemplateDto Hybrid,
    IReadOnlyList<EndpointComparisonItemDto> EndpointComparisons);

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
    IReadOnlyList<string> Warnings,
    int InputTokens = 0,
    int OutputTokens = 0,
    int TotalTokens = 0,
    int LlmApiCalls = 0);

public record AnalysisRunTemplateDto(
    Guid AnalysisRunId,
    BusinessTemplateSource Source,
    DateTime? UpdatedAt,
    int BusinessCount,
    int ApiCount,
    IReadOnlyList<BusinessTemplate> Businesses);

public enum BatchItemStatus
{
    Succeeded,
    Failed,
    /// <summary>Link trùng với một dòng phía trên, không xử lý lại.</summary>
    Skipped
}

public record BatchTemplateItemDto(int Row, string RepoUrl, BatchItemStatus Status, string? Error, GeneratedTemplateDto? Template);

public record BatchTemplateResultDto(int Total, int Succeeded, int Failed, int Skipped, IReadOnlyList<BatchTemplateItemDto> Items);
