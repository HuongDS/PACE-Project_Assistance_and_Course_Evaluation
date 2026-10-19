using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using PACE.Application.Common.Exceptions;
using PACE.Infrastructure.Llm;
using Xunit;

namespace PACE.Tests;

public class DeepSeekServiceTests
{
    private sealed class Handler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(++Calls));
    }
    private static HttpResponseMessage Response(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static DeepSeekService Service(Handler handler, string key = "test-key")
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DeepSeekConfig:ApiKey"] = key, ["DeepSeekConfig:RetryDelayMilliseconds"] = "0",
            ["DeepSeekConfig:MaxAttempts"] = "3"
        }).Build();
        return new DeepSeekService(Stub<IHttpClientFactory>.Create((_, _) => new HttpClient(handler)), config);
    }
    [Theory]
    [InlineData(401)] [InlineData(402)] [InlineData(403)]
    public async Task PermanentProviderErrorIsReportedWithoutRetryOrCredentialLeak(int status)
    {
        var handler = new Handler(_ => Response((HttpStatusCode)status, "secret provider body test-key"));
        var error = await Assert.ThrowsAsync<ExternalServiceException>(() => Service(handler).EvaluateWithLlmAsync("system", "answer"));
        Assert.Equal(1, handler.Calls);
        Assert.Contains(status.ToString(), error.Message);
        Assert.DoesNotContain("test-key", error.Message);
        Assert.DoesNotContain("secret provider body", error.Message);
    }
    [Fact]
    public async Task TemporaryFailureCanRetryAndReturnFeedback()
    {
        var handler = new Handler(n => n == 1 ? Response(HttpStatusCode.ServiceUnavailable, "unavailable")
            : Response(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"feedback\"}}]}"));
        Assert.Equal("feedback", await Service(handler).EvaluateWithLlmAsync("system", "answer"));
        Assert.Equal(2, handler.Calls);
    }
    [Fact]
    public async Task EmptyProviderContentFailsInsteadOfSilentlyReturningEmptyString()
    {
        var handler = new Handler(_ => Response(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"\"}}]}"));
        await Assert.ThrowsAsync<ExternalServiceException>(() => Service(handler).EvaluateWithLlmAsync("system", "answer"));
        Assert.Equal(1, handler.Calls);
    }
    [Fact]
    public async Task MissingKeyFailsBeforeSendingRequest()
    {
        var handler = new Handler(_ => throw new Exception("Must not send"));
        await Assert.ThrowsAsync<ExternalServiceException>(() => Service(handler, "").EvaluateWithLlmAsync("system", "answer"));
        Assert.Equal(0, handler.Calls);
    }
}
