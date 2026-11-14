using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PACE.Application.Common.Exceptions;
using PACE.Application.QuestionGeneration;
using PACE.Infrastructure.AI;
using Xunit;

namespace PACE.Tests;

public class GeminiQuestionLlmClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => respond(request, ++Calls);
    }

    private static HttpResponseMessage Response(int status, string body) => new((HttpStatusCode)status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static GeminiQuestionLlmClient Client(Handler handler, string key = "test-key", string model = "gemini-3.1-flash-lite") => new(
        new HttpClient(handler),
        Options.Create(new GeminiOptions { ApiKey = key, Model = model, InitialRetryDelaySeconds = 0, MaxRetries = 2 }),
        NullLogger<GeminiQuestionLlmClient>.Instance);

    private static string Completed(string content, string model = "gemini-3.1-flash-lite") => JsonSerializer.Serialize(new
    {
        candidates = new[]
        {
            new
            {
                content = new
                {
                    parts = new[] { new { text = content } },
                    role = "model"
                },
                finishReason = "STOP"
            }
        },
        usageMetadata = new
        {
            promptTokenCount = 120,
            candidatesTokenCount = 45,
            totalTokenCount = 165
        },
        modelVersion = model
    });

    [Fact]
    public async Task SendsGeminiRequestAndReturnsQuestionsWithUsage()
    {
        var handler = new Handler(async (request, _) =>
        {
            Assert.Contains("/v1beta/models/gemini-3.1-flash-lite:generateContent", request.RequestUri!.ToString());
            Assert.True(request.Headers.Contains("x-goog-api-key"));
            Assert.Equal("test-key", request.Headers.GetValues("x-goog-api-key").First());

            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var root = payload.RootElement;
            Assert.Equal(0.3f, root.GetProperty("generationConfig").GetProperty("temperature").GetSingle());
            Assert.Equal("application/json", root.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
            Assert.Equal("system instruction", root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
            Assert.Equal("prompt text", root.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());

            return Response(200, Completed("{\"questions\":[{\"question\":\"Tình huống?\",\"suggestedAnswer\":\"Đáp án\",\"difficulty\":\"Dễ\",\"targetedEntryPoints\":[\"A.B\"]}]}"));
        });

        var result = await Client(handler).GenerateJsonAsync("system instruction", "prompt text", 0.3f);
        Assert.Single(LlmQuestionParser.Parse(result.Text));
        Assert.Equal(120, result.InputTokens);
        Assert.Equal(45, result.OutputTokens);
        Assert.Equal("gemini-3.1-flash-lite", result.Model);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task RetriesTransientStatus(int status)
    {
        var handler = new Handler((_, n) => Task.FromResult(n == 1
            ? Response(status, "unavailable")
            : Response(200, Completed("{\"questions\":[]}"))));

        var result = await Client(handler).GenerateJsonAsync("s", "p", 0.3f);
        Assert.Equal(2, handler.Calls);
        Assert.Equal("{\"questions\":[]}", result.Text);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    public async Task PermanentErrorsDoNotRetryOrLeakProviderBody(int status)
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(status, "secret-key-internal-details")));
        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(handler).GenerateJsonAsync("s", "p", 0.3f));
        Assert.Contains(status.ToString(), ex.Message);
        Assert.DoesNotContain("secret-key", ex.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("{\"candidates\":[]}")]
    [InlineData("{\"candidates\":[{\"content\":{\"parts\":[]}}]}")]
    [InlineData("{\"candidates\":[{\"finishReason\":\"SAFETY\"}]}")]
    [InlineData("not json")]
    public async Task RejectsUnusableResponses(string body)
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(200, body)));
        await Assert.ThrowsAsync<ExternalServiceException>(() => Client(handler).GenerateJsonAsync("s", "p", 0.3f));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task MissingKeyAndCancellationDoNotSendRequests()
    {
        var handler = new Handler((_, _) => throw new Exception("Must not send"));
        await Assert.ThrowsAsync<ExternalServiceException>(() => Client(handler, "").GenerateJsonAsync("s", "p", 0.3f));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).GenerateJsonAsync("s", "p", 0.3f, cts.Token));
        Assert.Equal(0, handler.Calls);
    }
}
