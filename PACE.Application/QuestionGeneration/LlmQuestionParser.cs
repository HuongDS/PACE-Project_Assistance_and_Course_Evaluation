using System.Text.Json;
using System.Text.Json.Serialization;
using PACE.Application.Common.Exceptions;

namespace PACE.Application.QuestionGeneration;

/// <summary>Một câu hỏi đúng theo schema JSON mà prompt yêu cầu.</summary>
public class LlmQuestion
{
    [JsonPropertyName("question")]
    public string Question { get; set; } = string.Empty;

    [JsonPropertyName("suggestedAnswer")]
    public string? SuggestedAnswer { get; set; }

    [JsonPropertyName("difficulty")]
    public string? Difficulty { get; set; }

    [JsonPropertyName("targetedEntryPoints")]
    public List<string>? TargetedEntryPoints { get; set; }
}

/// <summary>Bóc mảng JSON câu hỏi từ phản hồi Gemini (cùng cách xử lý với Repo_Into_Graph).</summary>
public static class LlmQuestionParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static List<LlmQuestion> Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ExternalServiceException("Gemini", "Không trả về nội dung nào.");

        var text = StripCodeFence(raw.Trim());
        text = TrimToFirstJsonValue(text);

        try
        {
            // Đôi khi model bọc mảng trong một object, ví dụ { "questions": [...] }.
            using var doc = JsonDocument.Parse(text);
            var array = doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement,
                JsonValueKind.Object => doc.RootElement.EnumerateObject()
                    .Select(p => p.Value)
                    .FirstOrDefault(v => v.ValueKind == JsonValueKind.Array),
                _ => default
            };
            if (array.ValueKind != JsonValueKind.Array)
                throw new JsonException("Phản hồi không chứa mảng JSON câu hỏi.");

            var questions = array.Deserialize<List<LlmQuestion>>(Options) ?? [];
            return questions.Where(q => !string.IsNullOrWhiteSpace(q.Question)).ToList();
        }
        catch (JsonException ex)
        {
            var preview = text.Length > 500 ? text[..500] + "..." : text;
            throw new ExternalServiceException("Gemini", $"JSON trả về không hợp lệ: {ex.Message}. Nội dung: {preview}", ex);
        }
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```")) return text;
        var firstNewline = text.IndexOf('\n');
        if (firstNewline != -1) text = text[(firstNewline + 1)..];
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence != -1) text = text[..lastFence];
        return text.Trim();
    }

    /// <summary>
    /// Gemini thỉnh thoảng trả một mảng JSON hợp lệ rồi dán thêm ký tự thừa (vd. "]" lạc) ở cuối.
    /// Cắt tại ngoặc đóng khớp với ngoặc mở đầu tiên (bỏ qua ngoặc trong chuỗi).
    /// </summary>
    public static string TrimToFirstJsonValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        int start = raw.IndexOfAny(['[', '{']);
        if (start < 0) return raw;

        char open = raw[start];
        char close = open == '[' ? ']' : '}';
        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = start; i < raw.Length; i++)
        {
            char c = raw[i];
            if (escape) { escape = false; continue; }
            if (c == '\\') { if (inString) escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;

            if (c == open) depth++;
            else if (c == close && --depth == 0) return raw.Substring(start, i - start + 1);
        }

        return raw[start..];
    }
}
