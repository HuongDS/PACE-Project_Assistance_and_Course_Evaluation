using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PACE.Application.Common.Exceptions;
using PACE.Application.QuestionGeneration;
using PACE.Infrastructure.AI;
using Xunit;

namespace PACE.Tests;

public class OpenAiQuestionLlmClientTests
{
    private sealed class Handler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(++Calls));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ThrowsWhenApiKeyIsMissing()
    {
        var options = Options.Create(new OpenAiOptions { ApiKey = "" });
        var handler = new Handler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var client = new OpenAiQuestionLlmClient(options, new StubHttpClientFactory(new HttpClient(handler)), NullLogger<OpenAiQuestionLlmClient>.Instance);

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => client.GenerateJsonAsync("system", "prompt", 0.3f));
        Assert.Contains("Chưa cấu hình API key", ex.Message);
    }

    [Fact]
    public async Task SuccessfullyParsesOpenAiJsonResponse()
    {
        var json = """
        {
            "choices": [
                {
                    "message": {
                        "content": "[{\"question\": \"Test Question?\"}]"
                    }
                }
            ],
            "usage": {
                "prompt_tokens": 120,
                "completion_tokens": 80
            }
        }
        """;

        var options = Options.Create(new OpenAiOptions { ApiKey = "sk-test", Model = "gpt-4o-mini" });
        var handler = new Handler(_ => JsonResponse(HttpStatusCode.OK, json));
        var client = new OpenAiQuestionLlmClient(options, new StubHttpClientFactory(new HttpClient(handler)), NullLogger<OpenAiQuestionLlmClient>.Instance);

        var result = await client.GenerateJsonAsync("system", "prompt", 0.3f);

        Assert.Equal("[{\"question\": \"Test Question?\"}]", result.Text);
        Assert.Equal(120, result.InputTokens);
        Assert.Equal(80, result.OutputTokens);
        Assert.Equal("gpt-4o-mini", result.Model);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RetriesOnTransientErrorAndSucceeds()
    {
        var successJson = """
        {
            "choices": [
                {
                    "message": {
                        "content": "{\"status\":\"ok\"}"
                    }
                }
            ]
        }
        """;

        var options = Options.Create(new OpenAiOptions { ApiKey = "sk-test", InitialRetryDelaySeconds = 0 });
        var handler = new Handler(call => call == 1 ? JsonResponse(HttpStatusCode.ServiceUnavailable, "busy") : JsonResponse(HttpStatusCode.OK, successJson));
        var client = new OpenAiQuestionLlmClient(options, new StubHttpClientFactory(new HttpClient(handler)), NullLogger<OpenAiQuestionLlmClient>.Instance);

        var result = await client.GenerateJsonAsync("system", "prompt", 0.3f);

        Assert.Equal(2, handler.Calls);
        Assert.Equal("{\"status\":\"ok\"}", result.Text);
    }

    [Fact]
    public async Task RoutingClientFallsBackToGeminiWhenOpenAiKeyIsMissing()
    {
        var openAiOptions = Options.Create(new OpenAiOptions { ApiKey = "" }); // Chưa có key
        var qOptions = Options.Create(new QuestionGenerationOptions { Provider = "OpenAI" }); // Muốn dùng OpenAI

        var openAiHandler = new Handler(_ => JsonResponse(HttpStatusCode.OK, "{}"));
        var openAiClient = new OpenAiQuestionLlmClient(openAiOptions, new StubHttpClientFactory(new HttpClient(openAiHandler)), NullLogger<OpenAiQuestionLlmClient>.Instance);

        var geminiOptions = Options.Create(new GeminiOptions { ApiKey = "" });
        var geminiClient = new GeminiQuestionLlmClient(geminiOptions, NullLogger<GeminiQuestionLlmClient>.Instance);

        var router = new RoutingQuestionLlmClient(
            geminiClient,
            openAiClient,
            qOptions,
            openAiOptions,
            NullLogger<RoutingQuestionLlmClient>.Instance);

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => router.GenerateJsonAsync("system", "prompt", 0.3f));
        Assert.Equal("Gemini", ex.ServiceName);
        Assert.Equal(0, openAiHandler.Calls); // Không gọi OpenAI
    }
}
