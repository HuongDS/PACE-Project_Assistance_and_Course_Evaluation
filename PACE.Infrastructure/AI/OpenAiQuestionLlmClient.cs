using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PACE.Application.Common.Exceptions;
using PACE.Application.QuestionGeneration;

namespace PACE.Infrastructure.AI;

/// <summary>Sinh câu hỏi bằng OpenAI Responses API, giữ nguyên hợp đồng JSON của ứng dụng.</summary>
public sealed class OpenAiQuestionLlmClient(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiQuestionLlmClient> logger) : IQuestionLlmClient
{
    private readonly OpenAiOptions _options = options.Value;

    public async Task<LlmResult> GenerateJsonAsync(string systemInstruction, string prompt, float temperature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new ExternalServiceException("OpenAI", "Chưa cấu hình OpenAI:ApiKey (hoặc biến môi trường OpenAI__ApiKey).");

        // Structured Outputs requires an object root. The existing parser accepts { questions: [...] }.
        var payload = new
        {
            model = _options.Model,
            instructions = systemInstruction + "\nĐịnh dạng truyền tải: bọc mảng câu hỏi trong object JSON có key questions theo schema được cung cấp.",
            input = prompt,
            reasoning = new { effort = "none" }, // GPT-6 Luna supports temperature with reasoning disabled.
            temperature,
            store = false,
            text = new
            {
                format = new
                {
                    type = "json_schema", name = "generated_questions", strict = true,
                    schema = new
                    {
                        type = "object", additionalProperties = false,
                        required = new[] { "questions" },
                        properties = new
                        {
                            questions = new
                            {
                                type = "array",
                                items = new
                                {
                                    type = "object", additionalProperties = false,
                                    required = new[] { "question", "suggestedAnswer", "difficulty", "targetedEntryPoints" },
                                    properties = new
                                    {
                                        question = new { type = "string" },
                                        suggestedAnswer = new { type = "string" },
                                        difficulty = new { type = "string" },
                                        targetedEntryPoints = new { type = "array", items = new { type = "string" } }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var attempts = Math.Max(1, _options.MaxRetries);
        var delay = TimeSpan.FromSeconds(Math.Max(0, _options.InitialRetryDelaySeconds));
        var stopwatch = Stopwatch.StartNew();
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                request.Content = JsonContent.Create(payload);
                using var response = await httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    if (attempt < attempts && (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500))
                    {
                        var retryAfter = response.Headers.RetryAfter;
                        var retryDelay = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow) ?? delay;
                        await RetryAsync(attempt, retryDelay > TimeSpan.Zero ? retryDelay : delay, ct);
                        delay *= 2;
                        continue;
                    }
                    // Do not expose provider response bodies, which can contain submitted source code.
                    throw new ExternalServiceException("OpenAI", $"Gọi API thất bại: HTTP {(int)response.StatusCode}.");
                }

                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var root = document.RootElement;
                if (!root.TryGetProperty("status", out var status) || status.GetString() != "completed")
                    throw new ExternalServiceException("OpenAI", "API chưa hoàn thành phản hồi; không sử dụng câu hỏi bị cắt ngắn.");

                var parts = new List<string>();
                foreach (var item in root.GetProperty("output").EnumerateArray())
                {
                    if (item.GetProperty("type").GetString() != "message") continue;
                    foreach (var part in item.GetProperty("content").EnumerateArray())
                    {
                        var type = part.GetProperty("type").GetString();
                        if (type == "refusal")
                            throw new ExternalServiceException("OpenAI", "Model từ chối sinh câu hỏi cho nội dung này.");
                        if (type == "output_text") parts.Add(part.GetProperty("text").GetString() ?? "");
                    }
                }
                var text = string.Concat(parts);
                if (string.IsNullOrWhiteSpace(text))
                    throw new ExternalServiceException("OpenAI", "API không trả về nội dung văn bản nào.");
                var usage = root.TryGetProperty("usage", out var u) ? u : default;
                var inputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("input_tokens", out var i) ? i.GetInt32() : 0;
                var outputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("output_tokens", out var o) ? o.GetInt32() : 0;
                var model = root.TryGetProperty("model", out var m) ? m.GetString() ?? _options.Model : _options.Model;
                logger.LogInformation("OpenAI {Model}: {Latency} ms, input={Input} token, output={Output} token",
                    model, stopwatch.ElapsedMilliseconds, inputTokens, outputTokens);
                return new LlmResult(text, inputTokens, outputTokens, stopwatch.ElapsedMilliseconds, model);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= attempts)
                    throw new ExternalServiceException("OpenAI", "Không thể kết nối API hoặc đã hết thời gian chờ.", ex);
                await RetryAsync(attempt, delay, ct);
                delay *= 2;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                throw new ExternalServiceException("OpenAI", "Phản hồi API không hợp lệ.", ex);
            }
        }
        throw new ExternalServiceException("OpenAI", "Gọi API thất bại sau các lần thử lại.");
    }

    private Task RetryAsync(int attempt, TimeSpan delay, CancellationToken ct)
    {
        logger.LogWarning("OpenAI lỗi tạm thời. Thử lại sau {Delay}s (lần {Attempt}/{Max}).", delay.TotalSeconds, attempt, Math.Max(1, _options.MaxRetries));
        return Task.Delay(delay, ct);
    }
}
