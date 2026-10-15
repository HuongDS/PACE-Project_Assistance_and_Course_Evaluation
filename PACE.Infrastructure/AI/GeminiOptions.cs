namespace PACE.Infrastructure.AI;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>API key Gemini (free tier). Đặt trong appsettings.Development.json — KHÔNG commit.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Cùng model với Repo_Into_Graph / bài báo 1.</summary>
    public string Model { get; set; } = "gemini-3.1-flash-lite";

    /// <summary>Prompt lớn có thể sinh lâu hơn 100s (timeout mặc định của HttpClient).</summary>
    public int TimeoutSeconds { get; set; } = 600;

    public int MaxRetries { get; set; } = 5;

    public int InitialRetryDelaySeconds { get; set; } = 5;
}
