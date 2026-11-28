using System.Text.Json;
using PACE.API.Controllers;
using PACE.Application.Common.Exceptions;
using Xunit;

namespace PACE.Tests;

/// <summary>Đề xuất đề tài theo khung: nội dung soạn chung phải hiển thị an toàn và đủ các mục khung yêu cầu.</summary>
public class TopicProposalRulesTests
{
    private static readonly Guid TemplateId = Guid.Parse("5e7a0c11-0000-4000-8000-000000000001");

    private static readonly List<ProposalSection> Sections =
    [
        new("context", "Bối cảnh và bài toán", null, 5, true, "richtext"),
        new("refs", "Tài liệu tham khảo", null, 0, false, "richtext"),
        new("team", "Thành viên và phân công", null, 0, true, "team")
    ];

    private static readonly List<ProposalMember> Members =
    [
        new("SE180123", "Nguyễn Phúc Sanh", "Leader"),
        new("SE180456", "Trần Minh Khoa", "Member")
    ];

    private static JsonElement Doc(string json) => JsonDocument.Parse(json).RootElement;

    private static string Paragraph(string text) =>
        """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":""" + JsonSerializer.Serialize(text) + "}]}]}";

    [Fact]
    public void Text_Is_Encoded_And_Script_Never_Survives()
    {
        var html = TopicProposalRules.RenderHtml(Doc(Paragraph("<script>alert(1)</script> & <b>")));
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.StartsWith("<p>", html);
    }

    [Fact]
    public void Unknown_Nodes_Keep_Only_Their_Text()
    {
        var html = TopicProposalRules.RenderHtml(Doc("""
            {"type":"doc","content":[{"type":"iframe","attrs":{"src":"https://evil"},"content":[{"type":"text","text":"chữ"}]}]}
            """));
        Assert.Equal("chữ", html);
    }

    [Theory]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,hi", false)]
    [InlineData("/relative", false)]
    [InlineData("https://fpt.edu.vn/a?b=1", true)]
    [InlineData("mailto:gv@fpt.edu.vn", true)]
    public void Only_Web_And_Mail_Links_Are_Kept(string href, bool kept)
    {
        var json = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"link","marks":[{"type":"link","attrs":{"href":"""
            + JsonSerializer.Serialize(href) + "}}]}]}]}";
        var html = TopicProposalRules.RenderHtml(Doc(json));
        Assert.Equal(kept, html.Contains("<a href="));
        if (kept) Assert.Contains("rel=\"noopener noreferrer nofollow\"", html);
    }

    [Fact]
    public void Marks_And_Lists_Render_As_Html()
    {
        var html = TopicProposalRules.RenderHtml(Doc("""
            {"type":"doc","content":[
              {"type":"heading","attrs":{"level":3},"content":[{"type":"text","text":"Sinh viên"}]},
              {"type":"bulletList","content":[{"type":"listItem","content":[{"type":"paragraph","content":[
                {"type":"text","text":"Mua vé","marks":[{"type":"bold"},{"type":"italic"}]}]}]}]}]}
            """));
        Assert.Equal("<h4>Sinh viên</h4><ul><li><p><strong><em>Mua vé</em></strong></p></li></ul>", html);
    }

    [Fact]
    public void Words_In_Separate_Blocks_Are_Not_Joined()
    {
        var doc = Doc("""
            {"type":"doc","content":[
              {"type":"paragraph","content":[{"type":"text","text":"Một hai"}]},
              {"type":"paragraph","content":[{"type":"text","text":"ba"}]}]}
            """);
        Assert.Equal(3, TopicProposalRules.CountWords(doc));
    }

    [Fact]
    public void Complete_Content_Is_Normalized_With_Titles_From_The_Template()
    {
        var input = """{"sections":{"context":""" + Paragraph("Câu lạc bộ đang quản lý vé bằng bảng tính")
            + """},"team":{"SE180123":"Backend","SE180456":"Giao diện"}}""";
        var result = TopicProposalRules.BuildContent(input, TemplateId, 1, Sections, Members);

        Assert.Empty(result.Problems);
        using var json = JsonDocument.Parse(result.Json);
        var sections = json.RootElement.GetProperty("sections");
        Assert.Equal("Bối cảnh và bài toán", sections[0].GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, sections[1].GetProperty("doc").ValueKind);
        Assert.Equal("Giao diện", json.RootElement.GetProperty("team")[1].GetProperty("task").GetString());
        Assert.Contains("<h3>1. Bối cảnh và bài toán</h3>", result.Html);
        Assert.DoesNotContain("Tài liệu tham khảo", result.Html);
        // Mục không bắt buộc để trống bị bỏ, nên bảng thành viên là mục 2 chứ không phải 3.
        Assert.Contains("<h3>2. Thành viên và phân công</h3>", result.Html);
    }

    [Fact]
    public void Short_Required_Section_And_Missing_Task_Are_Reported()
    {
        var input = """{"sections":{"context":""" + Paragraph("Quá ngắn") + """},"team":{"SE180123":"Backend"}}""";
        var result = TopicProposalRules.BuildContent(input, TemplateId, 1, Sections, Members);

        Assert.Equal(2, result.Problems.Count);
        Assert.Contains(result.Problems, p => p.Contains("ít nhất 5 từ (hiện có 2)"));
        Assert.Contains(result.Problems, p => p.Contains("Trần Minh Khoa"));
    }

    [Fact]
    public void Task_Of_Someone_Outside_The_Group_Is_Ignored()
    {
        var input = """{"sections":{"context":""" + Paragraph("một hai ba bốn năm")
            + """},"team":{"SE180123":"A","SE180456":"B","SE999999":"Người lạ"}}""";
        var result = TopicProposalRules.BuildContent(input, TemplateId, 1, Sections, Members);
        Assert.DoesNotContain("SE999999", result.Json);
    }

    [Fact]
    public void Custom_Sections_Follow_The_Group_Order_And_Keep_Template_Rules()
    {
        var input = """{"sections":{"context":""" + Paragraph("một hai ba bốn năm")
            + ""","risk":""" + Paragraph("Thanh toán có thể chậm")
            + """},"team":{"SE180123":"A","SE180456":"B"},"custom":[{"key":"risk","title":"  Rủi ro   và cách xử lý "},{"key":"context","title":"Đè tên khung"},{"key":"<x>","title":"Key lạ"}],"order":["risk","context","team"]}""";
        var result = TopicProposalRules.BuildContent(input, TemplateId, 1, Sections, Members);

        Assert.Empty(result.Problems);
        using var json = JsonDocument.Parse(result.Json);
        var keys = json.RootElement.GetProperty("sections").EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();
        // refs không có trong order nên được chèn ngay sau context, đúng chỗ của nó trong khung.
        Assert.Equal(new[] { "risk", "context", "refs", "team" }, keys);
        var risk = json.RootElement.GetProperty("sections")[0];
        Assert.True(risk.GetProperty("custom").GetBoolean());
        Assert.Equal("Rủi ro và cách xử lý", risk.GetProperty("title").GetString());
        Assert.Equal("Bối cảnh và bài toán", json.RootElement.GetProperty("sections")[1].GetProperty("title").GetString());
        Assert.StartsWith("<section data-key=\"risk\"><h3>1. Rủi ro và cách xử lý</h3>", result.Html);
    }

    [Fact]
    public void Custom_Section_Without_Title_Is_Reported()
    {
        var input = """{"sections":{"context":""" + Paragraph("một hai ba bốn năm")
            + """},"team":{"SE180123":"A","SE180456":"B"},"custom":[{"key":"c1","title":"   "}]}""";
        var result = TopicProposalRules.BuildContent(input, TemplateId, 1, Sections, Members);
        Assert.Contains(result.Problems, p => p.Contains("chưa đặt tên"));
    }

    [Fact]
    public void Template_Sections_Missing_From_Order_Are_Placed_After_Their_Neighbour()
    {
        var order = TopicProposalRules.EffectiveSections(Sections, [("x", "X")], ["team", "x"]).Select(s => s.Key);
        Assert.Equal(new[] { "context", "refs", "team", "x" }, order);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Malformed_Content_Is_Rejected(string input) =>
        Assert.Throws<BadRequestException>(() => TopicProposalRules.BuildContent(input, TemplateId, 1, Sections, Members));

    [Theory]
    [InlineData("App", "Một câu tóm tắt đủ dài cho đề tài")]
    [InlineData("Ứng dụng bán vé", "Quá ngắn")]
    public void Short_Title_Or_Summary_Is_Rejected(string title, string summary) =>
        Assert.Throws<BadRequestException>(() => TopicProposalRules.CheckText(title, summary, null));

    [Fact]
    public void Text_Is_Trimmed_And_Collapsed()
    {
        var (title, summary, note) = TopicProposalRules.CheckText("  Ứng dụng   bán vé ", "Giúp CLB bán vé và điểm danh bằng QR", "  ");
        Assert.Equal("Ứng dụng bán vé", title);
        Assert.Equal("Giúp CLB bán vé và điểm danh bằng QR", summary);
        Assert.Null(note);
    }

    [Fact]
    public void Sections_Are_Read_From_Template_Json_Without_Duplicates()
    {
        var sections = TopicProposalRules.ParseSections("""
            [{"key":"context","title":"Bối cảnh","minWords":60,"required":true,"type":"richtext"},
             {"key":"context","title":"Trùng"},
             {"key":"team","title":"Thành viên","type":"team"},
             {"key":"refs","title":"Tham khảo","required":false}]
            """);
        Assert.Equal(new[] { "context", "team", "refs" }, sections.Select(s => s.Key));
        Assert.True(sections[1].IsTeam);
        Assert.False(sections[2].Required);
    }

    [Theory]
    [InlineData("EventWave", 2, "EventWave_DeXuat_v2.pdf")]
    [InlineData("Nhóm Đỉnh / Cao", 1, "Nhom_Dinh_Cao_DeXuat_v1.pdf")]
    [InlineData("///", 1, "Nhom_DeXuat_v1.pdf")]
    public void Pdf_File_Name_Is_Ascii(string group, int version, string expected) =>
        Assert.Equal(expected, TopicProposalRules.PdfFileName(group, version));

    [Theory]
    [InlineData(".png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, true)]
    [InlineData(".jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, true)]
    [InlineData(".png", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, false)]
    public void Attachment_Content_Must_Match_Its_Extension(string ext, byte[] bytes, bool ok) =>
        Assert.Equal(ok, TopicProposalRules.MatchesSignature(ext, bytes));
}
