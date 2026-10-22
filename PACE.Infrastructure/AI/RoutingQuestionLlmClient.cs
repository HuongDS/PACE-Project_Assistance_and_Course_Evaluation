using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PACE.Application.QuestionGeneration;

namespace PACE.Infrastructure.AI;

/// <summary>
/// Điều hướng gọi LLM sinh câu hỏi dựa trên cấu hình:
/// - Nếu cấu hình Provider = "OpenAI" và đã có ApiKey -> gọi OpenAI (GPT-4o / GPT-4o-mini).
/// - Nếu chưa cấu hình ApiKey hoặc Provider = "Gemini" -> tự động chuyển/giữ Gemini để hệ thống hoạt động liên tục, không bị lỗi.
/// </summary>
public sealed class RoutingQuestionLlmClient : IQuestionLlmClient
{
    private readonly GeminiQuestionLlmClient _geminiClient;
    private readonly OpenAiQuestionLlmClient _openAiClient;
    private readonly IOptions<QuestionGenerationOptions> _qOptions;
    private readonly IOptions<OpenAiOptions> _openAiOptions;
    private readonly ILogger<RoutingQuestionLlmClient> _logger;

    public RoutingQuestionLlmClient(
        GeminiQuestionLlmClient geminiClient,
        OpenAiQuestionLlmClient openAiClient,
        IOptions<QuestionGenerationOptions> qOptions,
        IOptions<OpenAiOptions> openAiOptions,
        ILogger<RoutingQuestionLlmClient> logger)
    {
        _geminiClient = geminiClient;
        _openAiClient = openAiClient;
        _qOptions = qOptions;
        _openAiOptions = openAiOptions;
        _logger = logger;
    }

    public async Task<LlmResult> GenerateJsonAsync(string systemInstruction, string prompt, float temperature, CancellationToken ct = default)
    {
        var provider = _qOptions.Value.Provider;
        var hasOpenAiKey = !string.IsNullOrWhiteSpace(_openAiOptions.Value.ApiKey);

        if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (hasOpenAiKey)
            {
                _logger.LogInformation("Đang sinh câu hỏi bằng OpenAI ({Model})...", _openAiOptions.Value.Model);
                return await _openAiClient.GenerateJsonAsync(systemInstruction, prompt, temperature, ct);
            }

            _logger.LogWarning("Cấu hình chọn Provider=OpenAI nhưng chưa điền ApiKey ({Section}:ApiKey). Tự động fallback về Gemini...", OpenAiOptions.SectionName);
        }

        _logger.LogInformation("Đang sinh câu hỏi bằng Gemini...");
        return await _geminiClient.GenerateJsonAsync(systemInstruction, prompt, temperature, ct);
    }
}
