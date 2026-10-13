namespace PACE.BLL.Common;

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Error { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public IDictionary<string, string[]>? ValidationErrors { get; set; }
}
