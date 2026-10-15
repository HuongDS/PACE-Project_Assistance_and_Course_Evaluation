using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PACE.Application.Services.Interfaces;

namespace PACE.Infrastructure.Llm
{
    public class DeepSeekService(IHttpClientFactory httpClient, IConfiguration configuration) : ILlmService
    {
        private readonly string _deepSeekApiKey = configuration["DeepSeekConfig:ApiKey"] ?? string.Empty;
        public async Task<string> EvaluateWithLlmAsync(string systemPrompt, string userPrompt)
        {
            var client = httpClient.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _deepSeekApiKey);

            var requestBody = new
            {
                model = "deepseek-chat",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.2,
                response_format = new { type = "json_object" }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            int maxRetries = 5;
            int delaySeconds = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Deepseak endpoint
                    var response = await client.PostAsync("https://api.deepseek.com/chat/completions", jsonContent);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMsg = await response.Content.ReadAsStringAsync();

                        // Rate Limit (429)
                        if ((int)response.StatusCode == 429)
                        {
                            if (attempt == maxRetries) throw new Exception($"DeepSeek Rate Limit exceeded: {errorMsg}");
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                            delaySeconds *= 2;
                            continue;
                        }

                        response.EnsureSuccessStatusCode();
                    }

                    var responseString = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(responseString);

                    var contentText = result
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    return contentText ?? string.Empty;
                }
                catch (Exception)
                {
                    if (attempt == maxRetries) return string.Empty;

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }

            return string.Empty;
        }
    }
}
