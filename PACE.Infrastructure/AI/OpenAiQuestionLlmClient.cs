using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PACE.Application.Common.Exceptions;
using PACE.Application.QuestionGeneration;

namespace PACE.Infrastructure.AI;

/// <summary>
/// Gọi OpenAI API (/chat/completions) để sinh câu hỏi vấn đáp với JSON Mode (response_format: json_object).
/// </summary>
public sealed class OpenAiQuestionLlmClient : IQuestionLlmClient
{
    private readonly OpenAiOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiQuestionLlmClient> _logger;

    public OpenAiQuestionLlmClient(
        IOptions<OpenAiOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiQuestionLlmClient> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<LlmResult> GenerateJsonAsync(string systemInstruction, string prompt, float temperature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new ExternalServiceException("OpenAI", $"Chưa cấu hình API key OpenAI ({OpenAiOptions.SectionName}:ApiKey trong appsettings.Development.json).");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Max(30, _options.TimeoutSeconds));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());

        var url = _options.Endpoint.TrimEnd('/') + "/chat/completions";

        var requestBody = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = prompt }
            },
            temperature = temperature,
            response_format = new { type = "json_object" }
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var maxRetries = Math.Max(1, _options.MaxRetries);
        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.InitialRetryDelaySeconds));
        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? responseBody = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                response = await client.PostAsync(url, jsonContent, ct);
                responseBody = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                    break;

                var isTransient = (int)response.StatusCode == 429 || (int)response.StatusCode >= 500;
                if (attempt < maxRetries && isTransient)
                {
                    _logger.LogWarning("OpenAI lỗi HTTP {Status}. Thử lại sau {Delay}s (lần {Attempt}/{Max}).",
                        response.StatusCode, delay.TotalSeconds, attempt, maxRetries);
                    await Task.Delay(delay, ct);
                    delay *= 2;
                    continue;
                }

                throw new ExternalServiceException("OpenAI", $"API trả về lỗi {response.StatusCode}: {responseBody}");
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxRetries && (ex is HttpRequestException || ex is TaskCanceledException))
            {
                _logger.LogWarning("OpenAI lỗi mạng ({Error}). Thử lại sau {Delay}s (lần {Attempt}/{Max}).",
                    ex.Message, delay.TotalSeconds, attempt, maxRetries);
                await Task.Delay(delay, ct);
                delay *= 2;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                throw new ExternalServiceException("OpenAI", $"Gọi API OpenAI thất bại: {ex.Message}", ex);
            }
        }

        stopwatch.Stop();

        if (string.IsNullOrWhiteSpace(responseBody))
            throw new ExternalServiceException("OpenAI", "API không trả về nội dung nào.");

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorProp))
        {
            var msg = errorProp.TryGetProperty("message", out var m) ? m.GetString() : errorProp.ToString();
            throw new ExternalServiceException("OpenAI", $"Lỗi từ OpenAI API: {msg}");
        }

        var text = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new ExternalServiceException("OpenAI", "Nội dung phản hồi từ OpenAI rỗng.");

        var inputTokens = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usageProp))
        {
            if (usageProp.TryGetProperty("prompt_tokens", out var pt)) inputTokens = pt.GetInt32();
            if (usageProp.TryGetProperty("completion_tokens", out var ctProp)) outputTokens = ctProp.GetInt32();
        }

        _logger.LogInformation("OpenAI {Model}: {Latency} ms, input={Input} token, output={Output} token",
            _options.Model, stopwatch.ElapsedMilliseconds, inputTokens, outputTokens);

        return new LlmResult(text, inputTokens, outputTokens, stopwatch.ElapsedMilliseconds, _options.Model);
    }
}
