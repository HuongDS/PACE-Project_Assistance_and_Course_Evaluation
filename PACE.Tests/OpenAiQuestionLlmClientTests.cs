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

public class OpenAiQuestionLlmClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, int, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => respond(request, ++Calls);
    }

    private static HttpResponseMessage Response(int status, string body) => new((HttpStatusCode)status)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static OpenAiQuestionLlmClient Client(Handler handler, string key = "test-key") => new(
        new HttpClient(handler), Options.Create(new OpenAiOptions { ApiKey = key, InitialRetryDelaySeconds = 0, MaxRetries = 2 }),
        NullLogger<OpenAiQuestionLlmClient>.Instance);

    private static string Completed(string content) => JsonSerializer.Serialize(new
    {
        status = "completed", model = "gpt-6-luna", usage = new { input_tokens = 100, output_tokens = 40 },
        output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = content } } } }
    });

    [Fact]
    public async Task SendsLunaRequestAndReturnsQuestionsWithUsage()
    {
        var handler = new Handler(async (request, _) =>
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var root = payload.RootElement;
            Assert.Equal("gpt-6-luna", root.GetProperty("model").GetString());
            Assert.Equal("none", root.GetProperty("reasoning").GetProperty("effort").GetString());
            Assert.Equal(0.3f, root.GetProperty("temperature").GetSingle());
            Assert.False(root.GetProperty("store").GetBoolean());
            Assert.True(root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
            return Response(200, Completed("{\"questions\":[{\"question\":\"Tình huống?\",\"suggestedAnswer\":\"Đáp án\",\"difficulty\":\"Dễ\",\"targetedEntryPoints\":[\"A.B\"]}]}"));
        });
        var result = await Client(handler).GenerateJsonAsync("system", "prompt", 0.3f);
        Assert.Single(LlmQuestionParser.Parse(result.Text));
        Assert.Equal(100, result.InputTokens);
        Assert.Equal(40, result.OutputTokens);
        Assert.Equal("gpt-6-luna", result.Model);
    }

    [Theory]
    [InlineData(429)] [InlineData(500)] [InlineData(503)]
    public async Task RetriesTransientStatus(int status)
    {
        var handler = new Handler((_, n) => Task.FromResult(n == 1 ? Response(status, "unavailable") : Response(200, Completed("{\"questions\":[]}"))));
        await Client(handler).GenerateJsonAsync("system", "prompt", 0.3f);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(400)] [InlineData(401)] [InlineData(403)]
    public async Task PermanentErrorsDoNotRetryOrLeakProviderBody(int status)
    {
        var handler = new Handler((_, _) => Task.FromResult(Response(status, "secret test-key source-code")));
        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => Client(handler).GenerateJsonAsync("s", "p", 0.3f));
        Assert.Contains(status.ToString(), ex.Message);
        Assert.DoesNotContain("secret", ex.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("{\"status\":\"incomplete\",\"output\":[]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[]}")]
    [InlineData("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"No\"}]}]}")]
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
