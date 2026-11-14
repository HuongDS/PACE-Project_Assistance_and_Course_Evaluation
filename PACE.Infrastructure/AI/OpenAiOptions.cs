namespace PACE.Infrastructure.AI;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; set; } = 600;
    public int MaxRetries { get; set; } = 5;
    public int InitialRetryDelaySeconds { get; set; } = 5;
}
