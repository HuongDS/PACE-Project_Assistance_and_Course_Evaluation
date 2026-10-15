using System.Diagnostics;
using Google.GenAI;
using Google.GenAI.Types;
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
            return new Client(apiKey: _options.ApiKey, clientOptions: new ClientOptions { HttpClientFactory = () => _httpClient });
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
        var stopwatch = Stopwatch.StartNew();
        GenAI.GenerateContentResponse? response = null;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                response = await _client.Value.Models.GenerateContentAsync(
                    model: _options.Model,
                    contents: prompt,
                    config: config);
                break;
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
            {
                _logger.LogWarning("Gemini lỗi tạm thời ({Error}). Thử lại sau {Delay}s (lần {Attempt}/{Max}).",
                    ex.Message, delay.TotalSeconds, attempt, maxRetries);
                await Task.Delay(delay, ct);
                delay *= 2;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                throw new ExternalServiceException("Gemini", $"Gọi API thất bại: {ex.Message}", ex);
            }
        }

        stopwatch.Stop();

        var text = response?.Text;
        if (string.IsNullOrWhiteSpace(text))
            throw new ExternalServiceException("Gemini", "API không trả về nội dung văn bản nào.");

        var inputTokens = response!.UsageMetadata?.PromptTokenCount ?? 0;
        var outputTokens = response.UsageMetadata?.CandidatesTokenCount ?? 0;
        _logger.LogInformation("Gemini {Model}: {Latency} ms, input={Input} token, output={Output} token",
            _options.Model, stopwatch.ElapsedMilliseconds, inputTokens, outputTokens);

        return new LlmResult(text, inputTokens, outputTokens, stopwatch.ElapsedMilliseconds, _options.Model);
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex is ClientError ce && ce.StatusCode == 429) return true;
        if (ex is TaskCanceledException or HttpRequestException) return true;
        var message = ex.Message;
        return message.Contains("429") || message.Contains("500") || message.Contains("503")
               || message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)
               || message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase)
               || message.Contains("overloaded", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _httpClient.Dispose();
}
