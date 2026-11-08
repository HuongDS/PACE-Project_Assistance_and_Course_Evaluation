namespace PACE.Application.QuestionGeneration;

public class QuestionGenerationOptions
{
    public const string SectionName = "QuestionGeneration";

    /// <summary>
    /// Ngưỡng node cho mode Hybrid: luồng có N_w &gt; ngưỡng thì dùng Graph, ngược lại dùng Raw.
    /// Mặc định 10 theo kết quả phân tầng ở bài báo 1 (dưới 10 node, nén CFG mất 0.224 accuracy;
    /// trên 10 node mức mất giảm một bậc độ lớn).
    /// </summary>
    public int GraphNodeThreshold { get; set; } = 10;

    public int MaxQuestions { get; set; } = 20;

    /// <summary>Nhà cung cấp LLM để sinh câu hỏi: "Gemini" (mặc định) hoặc "OpenAI".</summary>
    public string Provider { get; set; } = "Gemini";

    /// <summary>Giống Repo_Into_Graph (GenerateUnifiedQuestionsAsync): 0.3.</summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>Thư mục lưu trữ tạm các sơ đồ XML Draw.io đã xử lý (App_Data/specifications).</summary>
    public string SpecificationStoragePath { get; set; } = "App_Data/specifications";
}
