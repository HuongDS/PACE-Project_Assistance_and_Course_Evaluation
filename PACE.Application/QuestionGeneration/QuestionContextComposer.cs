using System.Text;
using PACE.Application.Analysis.Models;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.ReadModels;
using PACE.Domain.Enums;

namespace PACE.Application.QuestionGeneration;

/// <summary>Hai khe ngữ cảnh của prompt sau khi router đã chọn Raw hay Graph.</summary>
public record QuestionContextSlots(string Code, string Flow, bool HasFrontend);

/// <summary>
/// Dựng ngữ cảnh đúng như thực nghiệm của bài báo: đếm node, ghép mã và sơ đồ Mermaid, rồi ẩn khe không dùng.
/// Dùng chung cho sinh câu hỏi theo nghiệp vụ và theo từng sinh viên để hai nơi không lệch nhau.
/// </summary>
public static class QuestionContextComposer
{
    // Chuỗi thay thế khe ngữ cảnh bị ẩn — giữ nguyên văn như Repo_Into_Graph (QuestionGenerate.cs).
    private const string RawModeContextPlaceholder =
        "Chế độ Truyền thống: Không cung cấp sơ đồ Mermaid Graph. Hãy phân tích trực tiếp từ Source Code.";
    private const string GraphModeCodePlaceholder =
        "Chế độ Graph: Không cung cấp mã nguồn thô. Hãy phân tích logic dựa hoàn toàn vào sơ đồ Mermaid Graph được cung cấp.";
    private const string NoFlowContext =
        "Không có luồng nghiệp vụ (Feature) nào được map với Business này.";

    public static QuestionContextSlots Compose(IReadOnlyList<MethodCode> methods, IReadOnlyList<FlowQuestionContext> flows, ContextRoute route)
    {
        var hasGraph = flows.Any(f => !string.IsNullOrWhiteSpace(f.MermaidGraph));
        if (route.ContextType == ContextType.Graph && !hasGraph)
            throw new BadRequestException("Nghiệp vụ này chưa có sơ đồ Mermaid nào nên không sinh được câu hỏi ở mode Graph. Hãy dùng Raw hoặc phân tích lại repo.");

        var codeBuilder = BuildCode(methods, flows);
        var contextBuilder = BuildFlowContext(flows);
        if (route.ContextType == ContextType.Raw) contextBuilder = RawModeContextPlaceholder;
        var frontendMethods = methods.Where(m =>
            m.ClassName.StartsWith(RepositoryScan.FrontendClassPrefix, StringComparison.Ordinal)).ToList();
        if (route.ContextType == ContextType.Graph)
            codeBuilder = frontendMethods.Count == 0 ? GraphModeCodePlaceholder : BuildCode(frontendMethods, flows);
        return new QuestionContextSlots(codeBuilder, contextBuilder, frontendMethods.Count > 0);
    }

    public static int CountNodes(IEnumerable<FlowQuestionContext> flows)
    {
        var nodes = new HashSet<Guid>();
        foreach (var flow in flows)
        {
            nodes.Add(flow.EntryMethodId);
            foreach (var edge in flow.Edges)
            {
                nodes.Add(edge.FromMethodId);
                nodes.Add(edge.ToMethodId);
            }
        }
        return nodes.Count;
    }

    /// <summary>Mã nguồn thô, xếp theo thứ tự xuất hiện trong luồng (entry → các bước), phần còn lại theo tên.</summary>
    private static string BuildCode(IReadOnlyList<MethodCode> methods, IReadOnlyList<FlowQuestionContext> flows)
    {
        var order = new Dictionary<Guid, int>();
        foreach (var flow in flows)
        {
            order.TryAdd(flow.EntryMethodId, order.Count);
            foreach (var edge in flow.Edges)
            {
                order.TryAdd(edge.FromMethodId, order.Count);
                order.TryAdd(edge.ToMethodId, order.Count);
            }
        }

        var sb = new StringBuilder();
        foreach (var method in methods.OrderBy(m => order.TryGetValue(m.Id, out var i) ? i : int.MaxValue))
        {
            sb.AppendLine($"// Class: {method.ClassName}, Method: {method.MethodName}");
            sb.AppendLine(method.SourceCode);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildFlowContext(IReadOnlyList<FlowQuestionContext> flows)
    {
        if (flows.Count == 0) return NoFlowContext + Environment.NewLine;

        var sb = new StringBuilder();
        foreach (var flow in flows)
        {
            sb.AppendLine($"### Tên luồng: {flow.Name}");
            if (!string.IsNullOrWhiteSpace(flow.MermaidGraph))
            {
                sb.AppendLine("Data Mermaid Diagram:");
                sb.AppendLine(flow.MermaidGraph);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
