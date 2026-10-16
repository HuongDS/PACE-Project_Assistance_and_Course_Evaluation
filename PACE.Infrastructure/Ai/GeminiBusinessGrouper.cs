using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Exceptions;

namespace PACE.Infrastructure.AI;

public sealed class GeminiBusinessGrouper : IBusinessGroupingAi, IDisposable
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    // Prompt gom nghiệp vụ nhỏ; không cần chờ lâu như timeout 600s dùng cho sinh câu hỏi.
    private const int MaxTimeoutSeconds = 120;

    private const string SystemPrompt = """
        Bạn là chuyên gia phân tích nghiệp vụ cho đồ án phần mềm của sinh viên.
        Đầu vào là danh sách API (endpoint) đã quét từ mã nguồn, mỗi API có id, controller, http_method, function, path.
        Hãy gom các API thành các NGHIỆP VỤ theo góc nhìn người dùng cuối.

        Quy tắc bắt buộc:
        1. Mỗi id phải nằm trong ĐÚNG MỘT nghiệp vụ. Không bỏ sót id, không lặp id, không bịa id không có trong đầu vào.
        2. Một nghiệp vụ là một chức năng người dùng hiểu được, ví dụ "Quản lý ngân sách", "Đăng nhập và đăng ký".
           Có thể gộp nhiều controller liên quan, hoặc tách một controller lớn nếu nó chứa các chức năng khác nhau.
        3. business_name: tiếng Việt, ngắn gọn (tối đa 60 ký tự), không trùng nhau, không dùng từ kỹ thuật như Controller, API, CRUD.
        4. description: một câu tiếng Việt mô tả nghiệp vụ, chỉ dựa vào tên hàm và route; không suy diễn chức năng không có.
        5. endpoint_ids: danh sách id thuộc nghiệp vụ.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly object ResponseSchema = new
    {
        type = "ARRAY",
        items = new
        {
            type = "OBJECT",
            properties = new
            {
                business_name = new { type = "STRING" },
                description = new { type = "STRING" },
                endpoint_ids = new { type = "ARRAY", items = new { type = "INTEGER" } }
            },
            required = new[] { "business_name", "endpoint_ids" }
        }
    };

    private readonly GeminiOptions _options;
    private readonly HttpClient _http;

    public GeminiBusinessGrouper(IOptions<GeminiOptions> options)
    {
        _options = options.Value;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 10, MaxTimeoutSeconds)) };
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<SuggestedBusiness>> GroupAsync(IReadOnlyList<TemplateEndpoint> endpoints, CancellationToken ct)
    {
        if (!IsConfigured) throw new ExternalServiceException("Gemini", "Chưa cấu hình Gemini:ApiKey.");

        var input = JsonSerializer.Serialize(endpoints.Select(e => new
        {
            id = e.Id, controller = e.Controller, http_method = e.HttpMethod, function = e.Function, path = e.Path
        }));

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = input } } } },
            generationConfig = new { temperature = 0.2, responseMimeType = "application/json", responseSchema = ResponseSchema }
        };

        var text = ExtractText(await SendWithRetryAsync(body, ct));
        var groups = JsonSerializer.Deserialize<List<GeminiGroup>>(text, JsonOptions)
                     ?? throw new ExternalServiceException("Gemini", "Phản hồi rỗng.");
        return groups
            .Select(g => new SuggestedBusiness(g.BusinessName ?? string.Empty, g.Description, g.EndpointIds ?? []))
            .ToList();
    }

    // 429 (hết hạn mức) và 5xx (model quá tải) thường chỉ tạm thời nên thử lại trước khi bỏ cuộc.
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    private async Task<string> SendWithRetryAsync(object body, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{BaseUrl}/models/{Uri.EscapeDataString(_options.Model)}:generateContent")
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Add("x-goog-api-key", _options.ApiKey);

            using var response = await _http.SendAsync(request, ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode) return raw;

            var status = (int)response.StatusCode;
            if ((status == 429 || status >= 500) && attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt], ct);
                continue;
            }
            throw new ExternalServiceException("Gemini", $"HTTP {status}: {Truncate(raw, 300)}");
        }
    }

    private static string ExtractText(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var builder = new StringBuilder();
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
        {
            foreach (var part in parts.EnumerateArray())
                if (part.TryGetProperty("text", out var t)) builder.Append(t.GetString());
        }

        var text = builder.ToString();
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start) throw new ExternalServiceException("Gemini", $"Phản hồi không phải mảng JSON: {Truncate(text, 300)}");
        return text[start..(end + 1)];
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "...";

    public void Dispose() => _http.Dispose();

    private sealed class GeminiGroup
    {
        [JsonPropertyName("business_name")] public string? BusinessName { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("endpoint_ids")] public List<int>? EndpointIds { get; set; }
    }
}
