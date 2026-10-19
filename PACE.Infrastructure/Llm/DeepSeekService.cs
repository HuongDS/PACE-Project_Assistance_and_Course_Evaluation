using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PACE.Application.Common.Exceptions;
using PACE.Application.Services.Interfaces;

namespace PACE.Infrastructure.Llm;

public class DeepSeekService(IHttpClientFactory httpClient, IConfiguration configuration) : ILlmService
{
    public async Task<string> EvaluateWithLlmAsync(string systemPrompt, string userPrompt)
    {
        var key = configuration["DeepSeekConfig:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new ExternalServiceException("DeepSeek", "Chưa cấu hình API key để tạo feedback.");

        using var client = httpClient.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("DeepSeekConfig:TimeoutSeconds", 45), 1, 120));
        var attempts = Math.Clamp(configuration.GetValue("DeepSeekConfig:MaxAttempts", 2), 1, 3);
        var delay = Math.Clamp(configuration.GetValue("DeepSeekConfig:RetryDelayMilliseconds", 1000), 0, 10000);
        var body = JsonSerializer.Serialize(new
        {
            model = configuration["DeepSeekConfig:Model"] ?? "deepseek-chat",
            messages = new[] { new { role = "system", content = systemPrompt }, new { role = "user", content = userPrompt } },
            temperature = 0.2, response_format = new { type = "json_object" }
        });
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                    if (retryable && attempt < attempts) { await Task.Delay(delay * attempt); continue; }
                    // Do not expose provider response bodies, prompts or credentials.
                    var message = (int)response.StatusCode switch
                    {
                        401 or 403 => "API key bị từ chối (HTTP " + (int)response.StatusCode + "). Quản trị viên cần kiểm tra cấu hình DeepSeek.",
                        402 => "Tài khoản không đủ số dư để tạo feedback (HTTP 402).",
                        429 => "Dịch vụ đang giới hạn lượt gọi (HTTP 429). Chưa tạo được feedback.",
                        _ => "Không tạo được feedback (HTTP " + (int)response.StatusCode + ")."
                    };
                    throw new ExternalServiceException("DeepSeek", message);
                }
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrWhiteSpace(content))
                    throw new ExternalServiceException("DeepSeek", "Dịch vụ trả về feedback rỗng.");
                return content;
            }
            catch (ExternalServiceException) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                if (attempt == attempts)
                    throw new ExternalServiceException("DeepSeek", "Kết nối thất bại hoặc quá thời gian tạo feedback.");
                await Task.Delay(delay * attempt);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                throw new ExternalServiceException("DeepSeek", "Dịch vụ trả về dữ liệu feedback không hợp lệ.");
            }
        }
        throw new ExternalServiceException("DeepSeek", "Chưa tạo được feedback.");
    }
}
