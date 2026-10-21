namespace PACE.Application.QuestionGeneration;

public record LlmResult(string Text, int InputTokens, int OutputTokens, long LatencyMs, string Model);

/// <summary>Gọi LLM (OpenAI) và trả về văn bản JSON thô. Cài đặt nằm ở Infrastructure.</summary>
public interface IQuestionLlmClient
{
    Task<LlmResult> GenerateJsonAsync(string systemInstruction, string prompt, float temperature, CancellationToken ct = default);
}
