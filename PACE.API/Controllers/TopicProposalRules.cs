using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PACE.Application.Common.Exceptions;

namespace PACE.API.Controllers;

/// <summary>Một mục trong khung đề xuất (đọc từ proposal_templates.sections).</summary>
public record ProposalSection(string Key, string Title, string? Hint, int MinWords, bool Required, string Type, bool Custom = false)
{
    public bool IsTeam => Type == "team";
}

/// <summary>Thành viên nhóm lúc nộp, để kiểm tra và ghi bảng phân công.</summary>
public record ProposalMember(string StudentCode, string? FullName, string Role);

public record ProposalAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>Kết quả chuẩn hoá nội dung nộp: JSON tự chứa tiêu đề các mục và HTML an toàn để hiển thị.</summary>
public record ProposalContent(string Json, string Html, List<string> Problems);

/// <summary>
/// Quy tắc của đề xuất đề tài theo khung. Viết thành hàm tĩnh để kiểm thử riêng, không cần DB.
/// Nội dung các mục là JSON của Tiptap (ProseMirror); server không tin HTML từ client mà tự dựng HTML
/// theo danh sách node/mark cho phép, nên không có đường nào chèn script vào trang của giảng viên.
/// </summary>
public static partial class TopicProposalRules
{
    public const int TitleMin = 5;
    public const int TitleMax = 255;
    public const int SummaryMin = 20;
    public const int SummaryMax = 300;
    public const int NoteMax = 1000;
    public const int ContentMaxBytes = 1024 * 1024;
    public const long PdfMaxBytes = 10 * 1024 * 1024;
    public const long AttachmentMaxBytes = 10 * 1024 * 1024;
    public const int AttachmentMaxCount = 5;
    /// <summary>File PDF + 5 file đính kèm + phần chữ.</summary>
    public const long MaxRequestBytes = PdfMaxBytes + AttachmentMaxCount * AttachmentMaxBytes + 2 * 1024 * 1024;

    private static readonly Dictionary<string, string> AttachmentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg"
    };

    public const int CustomMax = 20;
    public const int CustomTitleMax = 120;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex CustomKey();

    // ---------- Khung ----------

    public static List<ProposalSection> ParseSections(string sectionsJson)
    {
        var result = new List<ProposalSection>();
        using var doc = JsonDocument.Parse(sectionsJson);
        foreach (var s in doc.RootElement.EnumerateArray())
        {
            var key = Str(s, "key");
            if (string.IsNullOrWhiteSpace(key) || result.Any(r => r.Key == key)) continue;
            result.Add(new ProposalSection(
                key,
                Str(s, "title") ?? key,
                Str(s, "hint"),
                s.TryGetProperty("minWords", out var m) && m.ValueKind == JsonValueKind.Number ? Math.Max(0, m.GetInt32()) : 0,
                !s.TryGetProperty("required", out var r) || r.ValueKind != JsonValueKind.False,
                Str(s, "type") == "team" ? "team" : "richtext"));
        }
        return result;
    }

    /// <summary>
    /// Danh sách mục theo thứ tự nhóm sắp: mục của khung giữ tên và yêu cầu của khung, mục tự thêm không bắt buộc.
    /// Key lạ trong order thì bỏ; mục của khung chưa có trong order thì chèn ngay sau mục đứng trước nó trong khung;
    /// mục tự thêm chưa có trong order thì để cuối. Cùng thuật toán với effectiveSections ở frontend.
    /// </summary>
    public static List<ProposalSection> EffectiveSections(
        IReadOnlyList<ProposalSection> template, IEnumerable<(string Key, string Title)> custom, IEnumerable<string> order)
    {
        var byKey = new Dictionary<string, ProposalSection>();
        foreach (var s in template) byKey.TryAdd(s.Key, s);
        foreach (var (key, title) in custom.Take(CustomMax))
            byKey.TryAdd(key, new ProposalSection(key, title, null, 0, false, "richtext", Custom: true));

        var result = new List<ProposalSection>();
        var seen = new HashSet<string>();
        foreach (var key in order)
            if (byKey.TryGetValue(key, out var s) && seen.Add(key)) result.Add(s);

        for (var i = 0; i < template.Count; i++)
        {
            var s = template[i];
            if (seen.Contains(s.Key)) continue;
            var prev = template.Take(i).Reverse().FirstOrDefault(p => seen.Contains(p.Key));
            var at = prev is null ? 0 : result.FindIndex(r => r.Key == prev.Key) + 1;
            result.Insert(at, byKey[s.Key]);
            seen.Add(s.Key);
        }
        foreach (var (key, s) in byKey)
            if (seen.Add(key)) result.Add(s);
        return result;
    }

    // ---------- Chữ ----------

    /// <summary>Đếm từ trong tài liệu Tiptap: mỗi khối là một đoạn riêng để chữ của hai đoạn không dính nhau.</summary>
    public static int CountWords(JsonElement doc)
    {
        var text = PlainText(doc).Trim();
        return text.Length == 0 ? 0 : Whitespace().Split(text).Length;
    }

    public static string PlainText(JsonElement node)
    {
        var sb = new StringBuilder();
        AppendText(node, sb);
        return sb.ToString();
    }

    private static void AppendText(JsonElement node, StringBuilder sb)
    {
        if (node.ValueKind != JsonValueKind.Object) return;
        var type = Str(node, "type");
        if (type == "text") { sb.Append(Str(node, "text")); return; }
        if (type == "hardBreak") { sb.Append(' '); return; }
        if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            foreach (var child in content.EnumerateArray()) AppendText(child, sb);
        if (type is not null and not "doc") sb.Append(' ');
    }

    /// <summary>Dựng HTML từ JSON Tiptap theo danh sách cho phép; node lạ chỉ giữ lại chữ bên trong.</summary>
    public static string RenderHtml(JsonElement node)
    {
        var sb = new StringBuilder();
        RenderNode(node, sb, 0);
        return sb.ToString();
    }

    private static void RenderNode(JsonElement node, StringBuilder sb, int depth)
    {
        if (node.ValueKind != JsonValueKind.Object || depth > 40) return;
        var type = Str(node, "type");
        if (type == "text") { RenderText(node, sb); return; }
        if (type == "hardBreak") { sb.Append("<br>"); return; }
        if (type == "horizontalRule") { sb.Append("<hr>"); return; }

        var (open, close) = type switch
        {
            "paragraph" => ("<p>", "</p>"),
            "heading" => HeadingTags(node),
            "bulletList" => ("<ul>", "</ul>"),
            "orderedList" => (OrderedListOpen(node), "</ol>"),
            "listItem" => ("<li>", "</li>"),
            "blockquote" => ("<blockquote>", "</blockquote>"),
            "codeBlock" => ("<pre><code>", "</code></pre>"),
            _ => ("", "")
        };
        sb.Append(open);
        if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            foreach (var child in content.EnumerateArray()) RenderNode(child, sb, depth + 1);
        sb.Append(close);
    }

    private static (string, string) HeadingTags(JsonElement node)
    {
        // Trong đề xuất, tiêu đề do người dùng thêm luôn nằm dưới tiêu đề mục, nên chỉ dùng h4–h6.
        var level = node.TryGetProperty("attrs", out var a) && a.TryGetProperty("level", out var l) && l.ValueKind == JsonValueKind.Number
            ? Math.Clamp(l.GetInt32() + 1, 4, 6) : 4;
        return ($"<h{level}>", $"</h{level}>");
    }

    private static string OrderedListOpen(JsonElement node)
    {
        var start = node.TryGetProperty("attrs", out var a) && a.TryGetProperty("start", out var s) && s.ValueKind == JsonValueKind.Number
            ? s.GetInt32() : 1;
        return start > 1 ? $"<ol start=\"{start}\">" : "<ol>";
    }

    private static void RenderText(JsonElement node, StringBuilder sb)
    {
        var text = Enc(Str(node, "text") ?? "");
        if (!node.TryGetProperty("marks", out var marks) || marks.ValueKind != JsonValueKind.Array)
        {
            sb.Append(text);
            return;
        }
        var opens = new List<string>();
        var closes = new List<string>();
        foreach (var mark in marks.EnumerateArray())
        {
            switch (Str(mark, "type"))
            {
                case "bold": opens.Add("<strong>"); closes.Insert(0, "</strong>"); break;
                case "italic": opens.Add("<em>"); closes.Insert(0, "</em>"); break;
                case "underline": opens.Add("<u>"); closes.Insert(0, "</u>"); break;
                case "strike": opens.Add("<s>"); closes.Insert(0, "</s>"); break;
                case "code": opens.Add("<code>"); closes.Insert(0, "</code>"); break;
                case "link":
                    var href = mark.TryGetProperty("attrs", out var attrs) ? SafeHref(Str(attrs, "href")) : null;
                    if (href is null) break;
                    opens.Add($"<a href=\"{Enc(href)}\" target=\"_blank\" rel=\"noopener noreferrer nofollow\">");
                    closes.Insert(0, "</a>");
                    break;
            }
        }
        foreach (var o in opens) sb.Append(o);
        sb.Append(text);
        foreach (var c in closes) sb.Append(c);
    }

    /// <summary>Chỉ nhận liên kết web và email; javascript:, data:... bị bỏ.</summary>
    public static string? SafeHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;
        href = href.Trim();
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri)) return null;
        return uri.Scheme is "http" or "https" or "mailto" ? uri.AbsoluteUri : null;
    }

    // ---------- Bản nộp ----------

    public static (string Title, string Summary, string? Note) CheckText(string? title, string? summary, string? note)
    {
        var t = Collapse(title);
        var s = Collapse(summary);
        var n = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (t.Length < TitleMin) throw new BadRequestException($"Tên đề tài cần ít nhất {TitleMin} ký tự.");
        if (t.Length > TitleMax) throw new BadRequestException($"Tên đề tài tối đa {TitleMax} ký tự.");
        if (s.Length < SummaryMin) throw new BadRequestException($"Tóm tắt một câu cần ít nhất {SummaryMin} ký tự.");
        if (s.Length > SummaryMax) throw new BadRequestException($"Tóm tắt một câu tối đa {SummaryMax} ký tự.");
        if (n is { Length: > NoteMax }) throw new BadRequestException($"Lời nhắn cho giảng viên tối đa {NoteMax} ký tự.");
        return (t, s, n);
    }

    /// <summary>
    /// Ghép nội dung client gửi lên với khung và danh sách thành viên.
    /// Đầu vào: {"sections":{"key":{...doc Tiptap...}},"team":{"MSSV":"phần việc"}}.
    /// Tiêu đề mục lấy từ khung chứ không lấy từ client.
    /// </summary>
    public static ProposalContent BuildContent(
        string contentJson, Guid templateId, int templateVersion,
        IReadOnlyList<ProposalSection> sections, IReadOnlyList<ProposalMember> members)
    {
        if (Encoding.UTF8.GetByteCount(contentJson) > ContentMaxBytes)
            throw new BadRequestException("Nội dung đề xuất quá dài (tối đa 1 MB).");

        JsonDocument input;
        try { input = JsonDocument.Parse(contentJson, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException) { throw new BadRequestException("Nội dung đề xuất không đúng định dạng."); }

        using (input)
        {
            var root = input.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new BadRequestException("Nội dung đề xuất không đúng định dạng.");
            root.TryGetProperty("sections", out var inSections);
            root.TryGetProperty("team", out var inTeam);
            var problems = new List<string>();

            // Mục nhóm tự thêm: key an toàn, không trùng mục của khung, tên không trống.
            var custom = new List<(string Key, string Title)>();
            if (root.TryGetProperty("custom", out var inCustom) && inCustom.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in inCustom.EnumerateArray())
                {
                    var key = Str(c, "key");
                    if (key is null || !CustomKey().IsMatch(key) || sections.Any(s => s.Key == key) || custom.Any(x => x.Key == key)) continue;
                    if (custom.Count == CustomMax)
                    {
                        problems.Add($"Tối đa {CustomMax} mục tự thêm.");
                        break;
                    }
                    var title = Collapse(Str(c, "title"));
                    if (title.Length > CustomTitleMax) title = title[..CustomTitleMax];
                    if (title.Length == 0) problems.Add("Có mục tự thêm chưa đặt tên.");
                    custom.Add((key, title.Length == 0 ? "Mục mới" : title));
                }
            }
            var order = root.TryGetProperty("order", out var inOrder) && inOrder.ValueKind == JsonValueKind.Array
                ? inOrder.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.String).Select(o => o.GetString()!).ToList()
                : new List<string>();
            sections = EffectiveSections(sections, custom, order);

            var outSections = new JsonArray();
            var outTeam = new JsonArray();
            var html = new StringBuilder();
            var number = 0;

            // Số thứ tự chỉ tính các mục được in ra (mục không bắt buộc để trống thì bỏ), giống file PDF.
            foreach (var section in sections)
            {
                if (section.IsTeam)
                {
                    var missing = new List<string>();
                    var rows = new StringBuilder();
                    var i = 0;
                    foreach (var m in members)
                    {
                        var task = inTeam.ValueKind == JsonValueKind.Object && inTeam.TryGetProperty(m.StudentCode, out var t) && t.ValueKind == JsonValueKind.String
                            ? Collapse(t.GetString()) : "";
                        if (task.Length > 500) task = task[..500];
                        if (task.Length == 0) missing.Add(m.FullName ?? m.StudentCode);
                        outTeam.Add(new JsonObject { ["studentCode"] = m.StudentCode, ["fullName"] = m.FullName, ["role"] = m.Role, ["task"] = task });
                        rows.Append($"<tr><td>{++i}</td><td>{Enc(m.FullName ?? "")}</td><td>{Enc(m.StudentCode)}</td><td>{Enc(task)}</td></tr>");
                    }
                    if (section.Required && missing.Count > 0)
                        problems.Add($"Mục \"{section.Title}\": chưa ghi phần việc của {string.Join(", ", missing)}.");
                    outSections.Add(new JsonObject { ["key"] = section.Key, ["title"] = section.Title, ["type"] = "team" });
                    html.Append($"<section data-key=\"{Enc(section.Key)}\"><h3>{++number}. {Enc(section.Title)}</h3>")
                        .Append("<table><thead><tr><th>TT</th><th>Họ tên</th><th>MSSV</th><th>Phần việc chính</th></tr></thead><tbody>")
                        .Append(rows).Append("</tbody></table></section>");
                    continue;
                }

                var doc = default(JsonElement);
                var has = inSections.ValueKind == JsonValueKind.Object
                    && inSections.TryGetProperty(section.Key, out doc) && doc.ValueKind == JsonValueKind.Object;
                var words = has ? CountWords(doc) : 0;
                if (section.Required && words < Math.Max(1, section.MinWords))
                    problems.Add(section.MinWords > 0
                        ? $"Mục \"{section.Title}\" cần ít nhất {section.MinWords} từ (hiện có {words})."
                        : $"Mục \"{section.Title}\" chưa có nội dung.");
                if (!has || words == 0)
                {
                    outSections.Add(new JsonObject
                    {
                        ["key"] = section.Key, ["title"] = section.Title, ["type"] = "richtext", ["custom"] = section.Custom, ["doc"] = null
                    });
                    if (section.Required)
                        html.Append($"<section data-key=\"{Enc(section.Key)}\"><h3>{++number}. {Enc(section.Title)}</h3></section>");
                    continue;
                }
                outSections.Add(new JsonObject
                {
                    ["key"] = section.Key, ["title"] = section.Title, ["type"] = "richtext", ["custom"] = section.Custom,
                    ["words"] = words, ["doc"] = JsonNode.Parse(doc.GetRawText())
                });
                html.Append($"<section data-key=\"{Enc(section.Key)}\"><h3>{++number}. {Enc(section.Title)}</h3>")
                    .Append(RenderHtml(doc)).Append("</section>");
            }

            var normalized = new JsonObject
            {
                ["templateId"] = templateId.ToString(),
                ["templateVersion"] = templateVersion,
                ["sections"] = outSections,
                ["team"] = outTeam
            };
            return new ProposalContent(normalized.ToJsonString(), html.ToString(), problems);
        }
    }

    // ---------- File ----------

    public static async Task<ProposalFile> ReadGeneratedPdfAsync(IFormFile? file, string fileName, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new BadRequestException("Thiếu file PDF của bản đề xuất.");
        if (file.Length > PdfMaxBytes) throw new BadRequestException("File PDF của bản đề xuất tối đa 10 MB.");
        var bytes = await ReadAsync(file, ct);
        if (!IsPdf(bytes)) throw new BadRequestException("File đề xuất gửi lên không phải PDF.");
        return new ProposalFile(fileName, "application/pdf", bytes);
    }

    public static async Task<List<ProposalAttachment>> ReadAttachmentsAsync(IReadOnlyList<IFormFile>? files, CancellationToken ct)
    {
        var result = new List<ProposalAttachment>();
        if (files is null) return result;
        if (files.Count > AttachmentMaxCount) throw new BadRequestException($"Tối đa {AttachmentMaxCount} tài liệu kèm theo.");
        foreach (var file in files)
        {
            var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
            if (name.Length > 255) name = name[^255..];
            if (file.Length == 0) throw new BadRequestException($"Tài liệu \"{name}\" đang trống.");
            if (file.Length > AttachmentMaxBytes) throw new BadRequestException($"Tài liệu \"{name}\" vượt quá 10 MB.");
            var ext = Path.GetExtension(name);
            if (!AttachmentTypes.TryGetValue(ext, out var type))
                throw new BadRequestException($"Tài liệu \"{name}\": chỉ nhận PDF, DOCX, PNG hoặc JPG.");
            var bytes = await ReadAsync(file, ct);
            if (!MatchesSignature(ext, bytes))
                throw new BadRequestException($"Nội dung tài liệu \"{name}\" không khớp đuôi file.");
            result.Add(new ProposalAttachment(name, type, bytes));
        }
        return result;
    }

    public static bool IsPdf(byte[] b) => b.Length > 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;

    public static bool MatchesSignature(string extension, byte[] b) => extension.ToLowerInvariant() switch
    {
        ".pdf" => IsPdf(b),
        ".docx" => b.Length > 4 && b[0] == 0x50 && b[1] == 0x4B && b[2] == 0x03 && b[3] == 0x04,
        ".png" => b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47,
        ".jpg" or ".jpeg" => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        _ => false
    };

    /// <summary>Tên file PDF: "EventWave_DeXuat_v2.pdf", bỏ dấu và ký tự không an toàn.</summary>
    public static string PdfFileName(string groupName, int version)
    {
        var normalized = groupName.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            var ch = c is 'đ' ? 'd' : c is 'Đ' ? 'D' : c;
            sb.Append(char.IsAsciiLetterOrDigit(ch) ? ch : '_');
        }
        var name = Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
        if (name.Length == 0) name = "Nhom";
        if (name.Length > 60) name = name[..60];
        return $"{name}_DeXuat_v{version}.pdf";
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        return stream.ToArray();
    }

    /// <summary>
    /// Mã hoá đủ cho nội dung và thuộc tính HTML. Không dùng WebUtility.HtmlEncode vì nó đổi cả chữ có dấu
    /// trong dải Latin-1 (à, é, ô...) thành mã số, làm HTML tiếng Việt khó đọc và nặng gấp nhiều lần.
    /// </summary>
    public static string Enc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    private static string Collapse(string? s) => string.IsNullOrWhiteSpace(s) ? "" : Whitespace().Replace(s.Trim(), " ");

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
