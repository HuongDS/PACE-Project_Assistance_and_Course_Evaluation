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
    private const int MaxTimeoutSeconds = 120;
    public const int HybridBatchSize = 12;

    private const string LegacyGroupSystemPrompt = """
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

    private const string HybridSliceSystemPrompt = """
        Bạn là hệ thống phân tích tĩnh & khôi phục ngữ nghĩa mã nguồn theo phương pháp Total Alpha-Renaming / Name-Agnostic Deobfuscation (Alon et al., POPL 2019).
        Trong đầu vào, toàn bộ tên controller, tên hàm và tên biến tự đặt của lập trình viên đã được ẩn danh hóa hoàn toàn thành fn_1, fn_2, VAR_1, VAR_2... để loại bỏ thiên kiến đặt tên.
        Mỗi API chỉ cung cấp các bằng chứng thực thi bất biến gồm:
        - http_method: phương thức HTTP.
        - context_graph_summary: gồm call_chain (fn_1 -> fn_2...) và callee_summaries (tóm tắt từ dưới lên của các hàm con).
        - deobfuscated_evidence_slice: các nút bằng chứng thực thi (ENTRY_DATA, DATA_FLOW, DB_READ, DB_WRITE, SDK_CALL, HTTP_OUT, EVENT_PUB, BRANCH, RETURN) kèm dfg_from và anchor.
        - code_anchors: các bảng/cột DB đọc/ghi và SDK bên ngoài không thể làm mờ.

        QUY TẮC BẮT BUỘC (GROUNDED GUARDRAILS):
        1. Suy luận 100% dựa trên chuỗi DFG, CFG (BRANCH/RETURN), callee_summaries và code_anchors (bảng DB, SDK, HTTP status). Không bao giờ nhắc tới các ký hiệu ẩn danh fn_1, VAR_1 trong câu mô tả tiếng Việt.
        2. Với mỗi endpoint_id trong đầu vào, trả về:
           - summary: 1 câu tiếng Việt ngắn gọn, chính xác mô tả chức năng thực sự của API dựa trên bảng DB và SDK mà nó tương tác.
           - behaviors: danh sách các mệnh đề hành vi (claim_id: "C1", "C2"..., action: câu tiếng Việt mô tả hành động/điều kiện cụ thể, evidence_nodes: mảng các id nút bằng chứng như ["n1", "n2"] có thật trong deobfuscated_evidence_slice của endpoint đó, confidence: độ tự tin 0.0 -> 1.0).
        """;

    private const string HybridGroupingSystemPrompt = """
        Bạn là chuyên gia phân tích nghiệp vụ phần mềm theo phương pháp Name-Agnostic Semantic Recovery.
        Đầu vào là danh sách các API đã được khôi phục ngữ nghĩa hoàn toàn từ đồ thị thực thi và neo cơ sở dữ liệu (mỗi API có id, http_method, summary, và code_anchors gồm db_reads, db_writes, external_sdks).
        Hãy gom các API thành các NGHIỆP VỤ theo góc nhìn người dùng cuối.

        Quy tắc bắt buộc:
        1. Dựa hoàn toàn vào `summary` và `code_anchors` (các bảng DB đọc/ghi và SDK dùng chung) để gom nhóm các API có liên quan mật thiết về mặt dữ liệu và luồng nghiệp vụ.
        2. Mỗi id phải nằm trong ĐÚNG MỘT nghiệp vụ. Không bỏ sót id, không lặp id, không bịa id không có trong đầu vào.
        3. business_name: tiếng Việt, ngắn gọn (tối đa 60 ký tự), không trùng nhau, không dùng từ kỹ thuật như Controller, API, CRUD.
        4. description: một câu tiếng Việt mô tả nghiệp vụ dựa trên summary của các API thành viên.
        5. endpoint_ids: danh sách id thuộc nghiệp vụ.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly object GroupResponseSchema = new
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

    private static readonly object HybridSliceResponseSchema = new
    {
        type = "ARRAY",
        items = new
        {
            type = "OBJECT",
            properties = new
            {
                endpoint_id = new { type = "INTEGER" },
                summary = new { type = "STRING" },
                behaviors = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            claim_id = new { type = "STRING" },
                            action = new { type = "STRING" },
                            evidence_nodes = new { type = "ARRAY", items = new { type = "STRING" } },
                            confidence = new { type = "NUMBER" }
                        },
                        required = new[] { "claim_id", "action", "evidence_nodes", "confidence" }
                    }
                }
            },
            required = new[] { "endpoint_id", "summary", "behaviors" }
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
        => (await GroupWithUsageAsync(endpoints, ct)).Groups;

    public async Task<LegacyGroupingResult> GroupWithUsageAsync(IReadOnlyList<TemplateEndpoint> endpoints, CancellationToken ct)
    {
        if (!IsConfigured) throw new ExternalServiceException("Gemini", "Chưa cấu hình Gemini:ApiKey.");

        var input = JsonSerializer.Serialize(endpoints.Select(e => new
        {
            id = e.Id, controller = e.Controller, http_method = e.HttpMethod, function = e.Function, path = e.Path
        }));

        var (groups, usage) = await RequestGroupsAsync(LegacyGroupSystemPrompt, input, ct);
        return new LegacyGroupingResult(groups, usage);
    }

    public async Task<HybridSynthesisResult> SynthesizeAndGroupAsync(
        IReadOnlyList<EndpointEvidenceSlice> slices,
        CancellationToken ct,
        int? batchSize = null)
    {
        if (!IsConfigured) throw new ExternalServiceException("Gemini", "Chưa cấu hình Gemini:ApiKey.");

        var effectiveBatchSize = Math.Clamp(batchSize ?? HybridBatchSize, 1, 50);
        var specsById = new Dictionary<int, SynthesizedEndpointSpec>();
        var totalUsage = LlmTokenUsage.Empty;
        var batchWarnings = new List<string>();
        var batches = slices.Chunk(effectiveBatchSize).ToList();
        // Gemini quá tải (đã retry hết) thì các batch sau gần như chắc chắn cũng lỗi:
        // dừng gọi, dùng fallback tĩnh cho phần còn lại và báo một dòng thay vì mỗi batch một dòng.
        GeminiOverloadedException? overloaded = null;
        var overloadedApiCount = 0;

        // Bước 1: Total Alpha-Renaming — Không gửi tên controller/function/path cho LLM để chống thiên kiến tên 100%
        // Cô lập lỗi theo từng batch (Fault-Tolerant Batching): nếu 1 batch lỗi sau khi retry, giữ nguyên toàn bộ token & kết quả các batch khác.
        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            if (overloaded is not null)
            {
                overloadedApiCount += batch.Length;
                continue;
            }

            var batchPayload = JsonSerializer.Serialize(batch.Select(s => new
            {
                endpoint_id = s.EndpointId,
                http_method = s.HttpMethod,
                context_graph_summary = s.ContextGraphSummary,
                deobfuscated_evidence_slice = s.EvidenceNodes,
                code_anchors = s.CodeAnchors
            }));

            var body = new
            {
                systemInstruction = new { parts = new[] { new { text = HybridSliceSystemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = batchPayload } } } },
                generationConfig = new { temperature = 0.1, responseMimeType = "application/json", responseSchema = HybridSliceResponseSchema }
            };

            try
            {
                var raw = await SendWithRetryAsync(body, ct);
                var (text, batchUsage) = ExtractTextAndUsage(raw);
                totalUsage = totalUsage.Add(batchUsage);

                var synthesizedBatch = JsonSerializer.Deserialize<List<GeminiEndpointSynthesis>>(text, JsonOptions) ?? [];
                foreach (var item in synthesizedBatch)
                {
                    specsById[item.EndpointId] = new SynthesizedEndpointSpec(
                        item.EndpointId,
                        item.Summary,
                        item.Behaviors);
                }
            }
            catch (GeminiOverloadedException ex) when (!ct.IsCancellationRequested)
            {
                overloaded = ex;
                overloadedApiCount += batch.Length;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                batchWarnings.Add($"Pha 4.1 Batch {i + 1}/{batches.Count} lỗi ({Truncate(ex.Message, 140)}), đã dùng fallback tĩnh cho {batch.Length} API này.");
            }
        }

        if (overloaded is not null)
        {
            batchWarnings.Add($"Gemini đang quá tải ({overloaded.Message}) nên {overloadedApiCount}/{slices.Count} API dùng mô tả tĩnh thay vì AI, " +
                              "và nghiệp vụ được gom theo controller. Kết quả vẫn dùng được; muốn AI mô tả lại thì thử lại sau ít phút.");
            return new HybridSynthesisResult([], specsById, totalUsage, batchWarnings);
        }

        // Bước 2: Gom nhóm các API thành Nghiệp vụ thuần túy dựa trên summary đã khôi phục + code_anchors (Name-Agnostic Grouping)
        var groupingInput = JsonSerializer.Serialize(slices.Select(s =>
        {
            var spec = specsById.GetValueOrDefault(s.EndpointId);
            return new
            {
                id = s.EndpointId,
                http_method = s.HttpMethod,
                summary = !string.IsNullOrWhiteSpace(spec?.Summary) ? spec.Summary : s.FallbackSummary,
                code_anchors = s.CodeAnchors
            };
        }));

        try
        {
            var (groups, groupUsage) = await RequestGroupsAsync(HybridGroupingSystemPrompt, groupingInput, ct);
            totalUsage = totalUsage.Add(groupUsage);
            return new HybridSynthesisResult(groups, specsById, totalUsage, batchWarnings);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            batchWarnings.Add($"Pha 4.2 gom nhóm nghiệp vụ lỗi ({Truncate(ex.Message, 140)}), đã giữ nguyên dữ liệu tổng hợp & gom theo controller.");
            return new HybridSynthesisResult([], specsById, totalUsage, batchWarnings);
        }
    }

    private async Task<(IReadOnlyList<SuggestedBusiness> Groups, LlmTokenUsage Usage)> RequestGroupsAsync(
        string systemPrompt, string userJson, CancellationToken ct)
    {
        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userJson } } } },
            generationConfig = new { temperature = 0.2, responseMimeType = "application/json", responseSchema = GroupResponseSchema }
        };

        var raw = await SendWithRetryAsync(body, ct);
        var (text, usage) = ExtractTextAndUsage(raw);
        var groups = JsonSerializer.Deserialize<List<GeminiGroup>>(text, JsonOptions)
                     ?? throw new ExternalServiceException("Gemini", "Phản hồi rỗng.");
        return (groups
            .Select(g => new SuggestedBusiness(g.BusinessName ?? string.Empty, g.Description, g.EndpointIds ?? []))
            .ToList(), usage);
    }

    private const int MaxRetryDelaySeconds = 60;

    /// <summary>Gemini trả 429/5xx hoặc không phản hồi sau khi đã retry hết: lỗi tạm thời phía Google, không phải lỗi request.</summary>
    private sealed class GeminiOverloadedException(string message) : ExternalServiceException("Gemini", message);

    /// <summary>Gọi model chính; model quá tải sau khi retry hết thì thử model dự phòng (nếu có cấu hình).</summary>
    private async Task<string> SendWithRetryAsync(object body, CancellationToken ct)
    {
        try
        {
            return await SendToModelAsync(_options.Model, body, ct);
        }
        catch (GeminiOverloadedException) when (!string.IsNullOrWhiteSpace(_options.FallbackModel)
                                               && !string.Equals(_options.FallbackModel, _options.Model, StringComparison.OrdinalIgnoreCase))
        {
            return await SendToModelAsync(_options.FallbackModel!, body, ct);
        }
    }

    private async Task<string> SendToModelAsync(string model, object body, CancellationToken ct)
    {
        var maxRetries = Math.Clamp(_options.MaxRetries, 0, 8);
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{BaseUrl}/models/{Uri.EscapeDataString(model)}:generateContent")
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Add("x-goog-api-key", _options.ApiKey);

            try
            {
                using var response = await _http.SendAsync(request, ct);
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode) return raw;

                var status = (int)response.StatusCode;
                if (status == 429 || status >= 500)
                {
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(RetryDelay(attempt, response.Headers.RetryAfter?.Delta), ct);
                        continue;
                    }
                    throw new GeminiOverloadedException($"{model} HTTP {status}: {ErrorMessage(raw)}");
                }

                throw new ExternalServiceException("Gemini", $"HTTP {status}: {ErrorMessage(raw)}");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                if (attempt >= maxRetries)
                    throw new GeminiOverloadedException($"{model} không phản hồi sau {_http.Timeout.TotalSeconds:0} giây");
                await Task.Delay(RetryDelay(attempt, null), ct);
            }
        }
    }

    /// <summary>Chờ theo Retry-After của Google nếu có, không thì tăng dần 5s, 10s, 20s... (tối đa 60s) cộng chút ngẫu nhiên để các request không dồn cùng lúc.</summary>
    private TimeSpan RetryDelay(int attempt, TimeSpan? retryAfter)
    {
        if (retryAfter is { TotalSeconds: > 0 and <= 65 }) return retryAfter.Value;
        var initial = Math.Clamp(_options.InitialRetryDelaySeconds, 1, MaxRetryDelaySeconds);
        var seconds = Math.Min(initial * Math.Pow(2, attempt), MaxRetryDelaySeconds);
        return TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
    }

    /// <summary>Lấy câu thông báo trong JSON lỗi của Google thay vì in nguyên cả khối JSON ra cảnh báo.</summary>
    private static string ErrorMessage(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message)
                && message.GetString() is { Length: > 0 } text)
                return Truncate(text, 160);
        }
        catch (JsonException)
        {
        }
        return Truncate(raw, 160);
    }

    private static (string Text, LlmTokenUsage Usage) ExtractTextAndUsage(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var builder = new StringBuilder();
        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
        {
            foreach (var part in parts.EnumerateArray())
                if (part.TryGetProperty("text", out var t)) builder.Append(t.GetString());
        }

        int inputTokens = 0, outputTokens = 0, totalTokens = 0;
        if (doc.RootElement.TryGetProperty("usageMetadata", out var meta))
        {
            if (meta.TryGetProperty("promptTokenCount", out var pt) && pt.TryGetInt32(out var pVal))
                inputTokens = pVal;
            if (meta.TryGetProperty("candidatesTokenCount", out var ctProp) && ctProp.TryGetInt32(out var cVal))
                outputTokens = cVal;
            if (meta.TryGetProperty("thoughtsTokenCount", out var thProp) && thProp.TryGetInt32(out var thVal))
                outputTokens += thVal;
            if (meta.TryGetProperty("totalTokenCount", out var ttProp) && ttProp.TryGetInt32(out var tVal))
                totalTokens = tVal;
            else
                totalTokens = inputTokens + outputTokens;
        }

        var text = builder.ToString();
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start) throw new ExternalServiceException("Gemini", $"Phản hồi không phải mảng JSON: {Truncate(text, 300)}");
        return (text[start..(end + 1)], new LlmTokenUsage(inputTokens, outputTokens, totalTokens, 1));
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "...";

    public void Dispose() => _http.Dispose();

    private sealed class GeminiGroup
    {
        [JsonPropertyName("business_name")] public string? BusinessName { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("endpoint_ids")] public List<int>? EndpointIds { get; set; }
    }

    private sealed class GeminiEndpointSynthesis
    {
        [JsonPropertyName("endpoint_id")] public int EndpointId { get; set; }
        [JsonPropertyName("summary")] public string? Summary { get; set; }
        [JsonPropertyName("behaviors")] public List<ApiBehaviorClaim>? Behaviors { get; set; }
    }
}
