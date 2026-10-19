using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PACE.Application.Services.Interfaces;

namespace PACE.Infrastructure.Llm
{
    public class GroqService(IHttpClientFactory httpClient, IConfiguration configuration) : IGroqService
    {
        private readonly string _groqApiKey = configuration["GroqConfig:ApiKey"] ?? string.Empty;
        private readonly string _defaultModel = configuration["GroqConfig:Model"] ?? "openai/gpt-oss-120b";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_groqApiKey);

        public async Task<string> EvaluateWithGroqAsync(string systemPrompt, string userPrompt, string? model = null)
        {
            if (!IsConfigured) return string.Empty;

            var client = httpClient.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _groqApiKey);

            var targetModel = !string.IsNullOrWhiteSpace(model) ? model : _defaultModel;

            var requestBody = new
            {
                model = targetModel,
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

            int maxRetries = 3;
            int delaySeconds = 2;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", jsonContent);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMsg = await response.Content.ReadAsStringAsync();

                        // Rate Limit (429)
                        if ((int)response.StatusCode == 429)
                        {
                            if (attempt == maxRetries) throw new Exception($"Groq Rate Limit exceeded: {errorMsg}");
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                            delaySeconds *= 2;
                            continue;
                        }

                        // Fallback to openai/gpt-oss-120b if model not found
                        if ((int)response.StatusCode == 404 && targetModel != "openai/gpt-oss-120b")
                        {
                            return await EvaluateWithGroqAsync(systemPrompt, userPrompt, "openai/gpt-oss-120b");
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
