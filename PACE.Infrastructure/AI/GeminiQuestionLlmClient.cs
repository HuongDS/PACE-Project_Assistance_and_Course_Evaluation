using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PACE.Application.Common.Exceptions;
using PACE.Application.QuestionGeneration;

namespace PACE.Infrastructure.AI;

/// <summary>Sinh câu hỏi bằng Google Gemini REST API (v1beta), trả về văn bản JSON thô và số liệu tokens.</summary>
public sealed class GeminiQuestionLlmClient(
    HttpClient httpClient,
    IOptions<GeminiOptions> options,
    ILogger<GeminiQuestionLlmClient> logger) : IQuestionLlmClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private readonly GeminiOptions _options = options.Value;

    public async Task<LlmResult> GenerateJsonAsync(string systemInstruction, string prompt, float temperature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new ExternalServiceException("Gemini", "Chưa cấu hình Gemini:ApiKey (hoặc biến môi trường Gemini__ApiKey).");

        object payload = string.IsNullOrWhiteSpace(systemInstruction)
            ? new
            {
                contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    temperature,
                    responseMimeType = "application/json"
                }
            }
            : new
            {
                systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    temperature,
                    responseMimeType = "application/json"
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
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{BaseUrl}/models/{Uri.EscapeDataString(_options.Model)}:generateContent");
                request.Headers.Add("x-goog-api-key", _options.ApiKey);
                request.Content = JsonContent.Create(payload);

                using var response = await httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var status = (int)response.StatusCode;
                    if (attempt < attempts && (response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500))
                    {
                        var retryAfter = response.Headers.RetryAfter;
                        var retryDelay = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow) ?? delay;
                        await RetryAsync(attempt, retryDelay > TimeSpan.Zero ? retryDelay : delay, ct);
                        delay *= 2;
                        continue;
                    }
                    throw new ExternalServiceException("Gemini", $"Gọi API thất bại: HTTP {status}.");
                }

                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var root = document.RootElement;

                if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                    throw new ExternalServiceException("Gemini", "API không trả về ứng viên phản hồi nào.");

                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
                {
                    var reason = finishReason.GetString();
                    if (reason == "SAFETY" || reason == "RECITATION")
                        throw new ExternalServiceException("Gemini", $"Model từ chối sinh câu hỏi: {reason}.");
                }

                var parts = new List<string>();
                if (firstCandidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var partsElement))
                {
                    foreach (var part in partsElement.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var t))
                        {
                            var s = t.GetString();
                            if (!string.IsNullOrEmpty(s)) parts.Add(s);
                        }
                    }
                }

                var text = string.Concat(parts);
                if (string.IsNullOrWhiteSpace(text))
                    throw new ExternalServiceException("Gemini", "API không trả về nội dung văn bản nào.");

                var usage = root.TryGetProperty("usageMetadata", out var u) ? u : default;
                var inputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("promptTokenCount", out var p) ? p.GetInt32() : 0;
                var outputTokens = usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty("candidatesTokenCount", out var c) ? c.GetInt32() : 0;
                var model = root.TryGetProperty("modelVersion", out var m) ? m.GetString() ?? _options.Model : _options.Model;

                logger.LogInformation("Gemini {Model}: {Latency} ms, input={Input} token, output={Output} token",
                    model, stopwatch.ElapsedMilliseconds, inputTokens, outputTokens);

                return new LlmResult(text, inputTokens, outputTokens, stopwatch.ElapsedMilliseconds, model);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= attempts)
                    throw new ExternalServiceException("Gemini", "Không thể kết nối API hoặc đã hết thời gian chờ.", ex);
                await RetryAsync(attempt, delay, ct);
                delay *= 2;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
            {
                throw new ExternalServiceException("Gemini", "Phản hồi API không hợp lệ.", ex);
            }
        }

        throw new ExternalServiceException("Gemini", "Gọi API thất bại sau các lần thử lại.");
    }

    private Task RetryAsync(int attempt, TimeSpan delay, CancellationToken ct)
    {
        logger.LogWarning("Gemini lỗi tạm thời. Thử lại sau {Delay}s (lần {Attempt}/{Max}).", delay.TotalSeconds, attempt, Math.Max(1, _options.MaxRetries));
        return Task.Delay(delay, ct);
    }
}
