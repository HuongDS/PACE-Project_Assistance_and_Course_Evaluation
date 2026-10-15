using System.Diagnostics;
using Google.GenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PACE.Application.Common.Exceptions;
using PACE.Application.QuestionGeneration;
using GenAI = Google.GenAI.Types;

namespace PACE.Infrastructure.AI;

/// <summary>
/// Gọi Gemini qua Google.GenAI SDK — cùng cấu hình với AIService.GenerateUnifiedQuestionsAsync
/// của Repo_Into_Graph: SystemInstruction + ResponseMimeType JSON, retry lùi luỹ thừa khi 429/500/503.
/// </summary>
public sealed class GeminiQuestionLlmClient : IQuestionLlmClient, IDisposable
{
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiQuestionLlmClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly Lazy<Client> _client;

    public GeminiQuestionLlmClient(IOptions<GeminiOptions> options, ILogger<GeminiQuestionLlmClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(30, _options.TimeoutSeconds)),
            DefaultRequestVersion = System.Net.HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        _client = new Lazy<Client>(() =>
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                throw new ExternalServiceException("Gemini", $"Chưa cấu hình API key ({GeminiOptions.SectionName}:ApiKey trong appsettings.Development.json).");
            return new Client(apiKey: _options.ApiKey, clientOptions: new GenAI.ClientOptions { HttpClientFactory = () => _httpClient });
        });
    }

    public async Task<LlmResult> GenerateJsonAsync(string systemInstruction, string prompt, float temperature, CancellationToken ct = default)
    {
        var config = new GenAI.GenerateContentConfig
        {
            SystemInstruction = new GenAI.Content { Parts = [new GenAI.Part { Text = systemInstruction }] },
            Temperature = temperature,
            ResponseMimeType = "application/json"
        };

        var maxRetries = Math.Max(1, _options.MaxRetries);
        var delay = TimeSpan.FromSeconds(Math.Max(1, _options.InitialRetryDelaySeconds));
        GenAI.GenerateContentResponse? response = null;
        long latencyMs = 0;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            // Đo latency riêng cho lần gọi thành công — không cộng thời gian chờ retry/backoff.
            var stopwatch = Stopwatch.StartNew();
            try
            {
                response = await _client.Value.Models.GenerateContentAsync(
                    model: _options.Model,
                    contents: prompt,
                    config: config,
                    cancellationToken: ct);
                stopwatch.Stop();
                latencyMs = stopwatch.ElapsedMilliseconds;
                break;
            }
            catch (AppException)
            {
                throw;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Người gọi huỷ request (client đóng kết nối) — không retry, không bọc lỗi.
                throw;
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
            {
                _logger.LogWarning("Gemini lỗi tạm thời ({Error}). Thử lại sau {Delay}s (lần {Attempt}/{Max}).",
                    ex.Message, delay.TotalSeconds, attempt, maxRetries);
                await Task.Delay(delay, ct);
                delay *= 2;
            }
            catch (TaskCanceledException ex)
            {
                // ct chưa bị huỷ => HttpClient.Timeout hết hạn.
                throw new ExternalServiceException("Gemini",
                    $"Quá thời gian chờ ({_httpClient.Timeout.TotalSeconds:0}s) khi gọi API.", ex);
            }
            catch (Exception ex)
            {
                throw new ExternalServiceException("Gemini", $"Gọi API thất bại: {ex.Message}", ex);
            }
        }

        var text = response?.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new ExternalServiceException("Gemini", "API không trả về nội dung văn bản nào.");

        var inputTokens = response!.UsageMetadata?.PromptTokenCount ?? 0;
        var outputTokens = response.UsageMetadata?.CandidatesTokenCount ?? 0;
        _logger.LogInformation("Gemini {Model}: {Latency} ms, input={Input} token, output={Output} token",
            _options.Model, latencyMs, inputTokens, outputTokens);

        return new LlmResult(text, inputTokens, outputTokens, latencyMs, _options.Model);
    }

    /// <summary>
    /// Chỉ retry lỗi thật sự tạm thời. Lưu ý: trong Google.GenAI, ClientError (4xx) và ServerError (5xx)
    /// đều kế thừa HttpRequestException, nên phải xét chúng TRƯỚC nhánh HttpRequestException chung —
    /// nếu không, 400/401/403/404 (sai key, sai model, prompt lỗi) cũng bị retry vô ích.
    /// Timeout của HttpClient (600s) không retry để request không treo tới hàng chục phút.
    /// </summary>
    private static bool IsTransient(Exception ex) => ex switch
    {
        ClientError ce => ce.StatusCode is 408 or 429,
        ServerError se => se.StatusCode is 500 or 502 or 503 or 504,
        TaskCanceledException => false,
        HttpRequestException hre => hre.StatusCode is null // lỗi mạng/kết nối, chưa có HTTP status
                                    || (int)hre.StatusCode.Value is 429 or >= 500,
        _ => ex.Message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
             || ex.Message.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase)
             || ex.Message.Contains("overloaded", StringComparison.OrdinalIgnoreCase)
    };

    public void Dispose() => _httpClient.Dispose();
}
