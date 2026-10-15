namespace PACE.Domain.Enums;

/// <summary>
/// Cách đưa ngữ cảnh mã nguồn vào prompt khi sinh câu hỏi.
/// Raw/Graph tương ứng arm Traditional/Pure CFG của bài báo ICIIT 2027;
/// Hybrid chọn một trong hai theo kích thước luồng (số node N_w).
/// </summary>
public enum QuestionGenerationMode
{
    /// <summary>Chỉ đưa mã nguồn thô, ẩn sơ đồ Mermaid.</summary>
    Raw,

    /// <summary>Chỉ đưa sơ đồ Mermaid, ẩn mã nguồn thô.</summary>
    Graph,

    /// <summary>N_w &gt; ngưỡng (mặc định 10) thì dùng Graph, ngược lại dùng Raw.</summary>
    Hybrid
}
