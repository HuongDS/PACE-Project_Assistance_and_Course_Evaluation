namespace PACE.Infrastructure.AI;

public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>API key của OpenAI (bắt đầu bằng sk-...).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Tên model GPT (mặc định gpt-4o-mini).</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Endpoint API (mặc định https://api.openai.com/v1).</summary>
    public string Endpoint { get; set; } = "https://api.openai.com/v1";

    public int TimeoutSeconds { get; set; } = 300;

    public int MaxRetries { get; set; } = 3;

    public int InitialRetryDelaySeconds { get; set; } = 2;
}
