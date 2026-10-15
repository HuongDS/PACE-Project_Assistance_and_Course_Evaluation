using System.ComponentModel.DataAnnotations;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos;

public class GenerateQuestionsRequest
{
    /// <summary>Raw | Graph | Hybrid (mặc định Hybrid: N_w &gt; 10 node dùng Graph, ngược lại Raw).</summary>
    public QuestionGenerationMode Mode { get; set; } = QuestionGenerationMode.Hybrid;

    [Range(1, 20)]
    public int NumberOfQuestions { get; set; } = 5;

    /// <summary>"Dễ" | "Trung bình" | "Khó" (khớp với hướng dẫn độ khó trong prompt).</summary>
    [Required, MaxLength(20)]
    public string Difficulty { get; set; } = "Trung bình";

    /// <summary>Hướng dẫn bổ sung từ giảng viên (tuỳ chọn).</summary>
    [MaxLength(4000)]
    public string? Description { get; set; }
}

public record GeneratedQuestionDto(
    Guid Id,
    Guid BusinessId,
    string QuestionText,
    string? SuggestedAnswer,
    string? Difficulty,
    ContextType ContextType,
    DateTime CreatedAt,
    IReadOnlyList<string>? TargetedEntryPoints = null);

public record GenerateQuestionsResponse(
    Guid BusinessId,
    string BusinessName,
    QuestionGenerationMode RequestedMode,
    ContextType AppliedContext,
    int NodeCount,
    int GraphNodeThreshold,
    string RoutingReason,
    int FlowCount,
    int MethodCount,
    string Model,
    int InputTokens,
    int OutputTokens,
    long LatencyMs,
    IReadOnlyList<GeneratedQuestionDto> Questions);
