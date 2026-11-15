using System.Text.RegularExpressions;

namespace PACE.Application.Analysis.Security;

/// <summary>Một chỗ đã che: tên luật và số dòng. Cố ý KHÔNG chứa giá trị bí mật.</summary>
public sealed record RedactionHit(string Rule, int Line);

public sealed record RedactionResult(string Content, IReadOnlyList<RedactionHit> Hits);

/// <summary>
/// Che thông tin nhạy cảm (khoá API, token, mật khẩu, chuỗi kết nối, khoá riêng) trong mã nguồn TRƯỚC khi
/// lưu xuống DB hoặc gửi sang LLM. Chỉ thay phần giá trị bằng <see cref="Placeholder"/>, giữ nguyên phần còn lại
/// của dòng để mã vẫn parse được. Kết quả idempotent: che lần hai không đổi gì.
/// Có giới hạn thời gian cho từng regex; hết giờ sẽ ném <see cref="RegexMatchTimeoutException"/> để bên gọi
/// bỏ hẳn file thay vì dùng bản chưa che.
/// </summary>
public static class SecretRedactor
{
    public const string Placeholder = "[REDACTED]";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // Tên khoá hay gặp khi gán bí mật. \b hai đầu để "PasswordError" không bị coi là "password".
    private const string SecretKeys =
        @"(?:api[_-]?key|apikey|secret(?:[_-]?key)?|client[_-]?secret|access[_-]?key|access[_-]?token|auth[_-]?token|refresh[_-]?token|token|password|passwd|pwd|private[_-]?key|connection[_-]?string)";

    private sealed record Rule(string Name, Regex Pattern, string? ValueGroup = null);

    // Luật cụ thể (nhận diện theo định dạng) chạy trước, luật theo tên khoá chạy sau.
    private static readonly Rule[] Rules =
    [
        new("private-key",
            new Regex(@"-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----[\s\S]*?-----END (?:[A-Z]+ )?PRIVATE KEY-----", Options, MatchTimeout)),
        new("aws-access-key", new Regex(@"\b(?:AKIA|ASIA)[0-9A-Z]{16}\b", Options, MatchTimeout)),
        new("github-token", new Regex(@"\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{22,})\b", Options, MatchTimeout)),
        new("slack-token", new Regex(@"\bxox[baprs]-[A-Za-z0-9-]{10,}", Options, MatchTimeout)),
        new("google-api-key", new Regex(@"\bAIza[0-9A-Za-z_\-]{35}\b", Options, MatchTimeout)),
        new("llm-api-key", new Regex(@"\bsk-[A-Za-z0-9_\-]{20,}", Options, MatchTimeout)),
        new("jwt", new Regex(@"\beyJ[A-Za-z0-9_\-]{10,}\.eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}", Options, MatchTimeout)),
        new("bearer-token",
            new Regex(@"(?i)\bbearer\s+(?<v>[A-Za-z0-9\-._~+/]{20,}=*)", Options, MatchTimeout), "v"),
        new("url-credentials",
            new Regex(@"(?<=://[^/\s:@""']+:)(?<v>[^@/\s""']+)(?=@)", Options, MatchTimeout), "v"),
        // Chỉ khớp dạng chuỗi kết nối: đứng sau ';' hoặc dấu nháy và viết liền "Password=giá_trị".
        // Không khớp phép gán trong code như "var password = request.Password;".
        new("connection-string-password",
            new Regex(@"(?<=[;""']\s*)(?i:password|pwd)=(?<v>[^;""'\s]+)", Options, MatchTimeout), "v"),
        new("secret-assignment",
            new Regex($@"(?i)\b{SecretKeys}\b[""']?\s*[:=]\s*[""'](?<v>[^\s""']{{8,}})[""']", Options, MatchTimeout), "v")
    ];

    public static RedactionResult Redact(string content)
    {
        if (string.IsNullOrEmpty(content)) return new RedactionResult(content, []);

        var hits = new List<RedactionHit>();
        foreach (var rule in Rules)
        {
            var original = content;
            content = rule.Pattern.Replace(content, match =>
            {
                var group = rule.ValueGroup is null ? match.Groups[0] : match.Groups[rule.ValueGroup];
                if (!ShouldRedact(group.Value)) return match.Value;

                hits.Add(new RedactionHit(rule.Name, LineOf(original, group.Index)));
                // Thay đúng phần giá trị, giữ nguyên phần còn lại của khớp.
                return string.Concat(
                    match.Value.AsSpan(0, group.Index - match.Index),
                    Placeholder,
                    match.Value.AsSpan(group.Index - match.Index + group.Length));
            });
        }

        return new RedactionResult(content, hits.OrderBy(h => h.Line).ToList());
    }

    /// <summary>Bỏ qua giá trị đã che, biến môi trường/placeholder (${X}, &lt;x&gt;, {{x}}) và giá trị toàn ký tự giống nhau.</summary>
    private static bool ShouldRedact(string value)
    {
        if (value.Contains(Placeholder, StringComparison.Ordinal)) return false;
        if (value.StartsWith("${", StringComparison.Ordinal) || value.StartsWith("$(", StringComparison.Ordinal)
            || value.StartsWith("%", StringComparison.Ordinal) && value.EndsWith("%", StringComparison.Ordinal)
            || value.StartsWith("{{", StringComparison.Ordinal)
            || value.StartsWith("<", StringComparison.Ordinal) && value.EndsWith(">", StringComparison.Ordinal)) return false;
        if (value.Distinct().Count() == 1 && value.Length < 40) return false;
        return true;
    }

    private static int LineOf(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < text.Length; i++)
            if (text[i] == '\n') line++;
        return line;
    }
}
