using PACE.Domain.Enums;

namespace PACE.Application.QuestionGeneration;

public record ContextRoute(ContextType ContextType, string Reason);

public static class QuestionContextRouter
{
    /// <summary>
    /// Chọn biểu diễn ngữ cảnh. Với Hybrid: N_w &gt; threshold → Graph, ngược lại → Raw.
    /// </summary>
    public static ContextRoute Resolve(QuestionGenerationMode mode, int nodeCount, int threshold) => mode switch
    {
        QuestionGenerationMode.Raw => new ContextRoute(ContextType.Raw, "Mode Raw: chỉ dùng mã nguồn thô."),
        QuestionGenerationMode.Graph => new ContextRoute(ContextType.Graph, "Mode Graph: chỉ dùng sơ đồ Mermaid."),
        _ => nodeCount > threshold
            ? new ContextRoute(ContextType.Graph, $"Mode Hybrid: luồng có {nodeCount} node > {threshold} nên dùng Graph (nén CFG gần như không mất độ chính xác ở luồng lớn).")
            : new ContextRoute(ContextType.Raw, $"Mode Hybrid: luồng có {nodeCount} node ≤ {threshold} nên dùng Raw (luồng nhỏ mất nhiều độ chính xác nhất khi nén CFG).")
    };
}
