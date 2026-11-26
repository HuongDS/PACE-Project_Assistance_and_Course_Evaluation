using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PACE.API.HealthChecks;

/// <summary>
/// Kiểm tra cấu hình API key của các AI provider. Chỉ đọc cấu hình, KHÔNG gọi ra ngoài để không tốn phí
/// và không phụ thuộc mạng; giá trị key không bao giờ được đưa vào kết quả.
/// Bắt buộc: provider sinh câu hỏi đang chọn (QuestionGeneration:Provider), Gemini (nhóm nghiệp vụ khi phân tích repo),
/// DeepSeek (feedback câu trả lời). Groq chỉ dùng cho chế độ Researching nên là tuỳ chọn.
/// </summary>
public sealed class AiProvidersHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var questionProvider = string.Equals(configuration["QuestionGeneration:Provider"], "OpenAI", StringComparison.OrdinalIgnoreCase)
            ? "OpenAI"
            : "Gemini";

        var providers = new (string Name, string Key, bool Required, string Purpose)[]
        {
            ("OpenAI", "OpenAI:ApiKey", questionProvider == "OpenAI", "sinh câu hỏi"),
            ("Gemini", "Gemini:ApiKey", true, questionProvider == "Gemini" ? "nhóm nghiệp vụ và sinh câu hỏi" : "nhóm nghiệp vụ"),
            ("DeepSeek", "DeepSeekConfig:ApiKey", true, "feedback câu trả lời"),
            ("Groq", "GroqConfig:ApiKey", false, "chế độ Researching")
        };

        var data = new Dictionary<string, object> { ["questionProvider"] = questionProvider };
        var missing = new List<string>();
        foreach (var (name, key, required, purpose) in providers)
        {
            var configured = !string.IsNullOrWhiteSpace(configuration[key]);
            data[name] = configured ? "configured" : "missing";
            if (!configured && required) missing.Add($"{name} ({purpose})");
        }

        return Task.FromResult(missing.Count == 0
            ? HealthCheckResult.Healthy("Các AI provider bắt buộc đều đã cấu hình.", data)
            : HealthCheckResult.Degraded($"Thiếu API key: {string.Join(", ", missing)}.", data: data));
    }
}
