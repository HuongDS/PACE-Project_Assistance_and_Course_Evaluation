using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PACE.Application.QuestionGeneration.Drawio;

public static partial class DrawioXmlParser
{
    [GeneratedRegex("<br\\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex("<.*?>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    public static DrawioProcessDiagram Parse(string rawInput, Guid businessId = default, string? diagramName = null)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            throw new ArgumentException("Nội dung XML Draw.io không được để trống.", nameof(rawInput));

        string? diagramNameFromRaw = null;
        try
        {
            var rawDoc = XDocument.Parse(rawInput.Trim());
            diagramNameFromRaw = (string?)rawDoc.Descendants("diagram").FirstOrDefault()?.Attribute("name");
        }
        catch
        {
            // Nếu parse XML thô thất bại (do chứa ký tự đặc biệt), thử tiếp tục sau giải nén
        }

        var uncompressedXml = DecompressIfNeeded(rawInput.Trim());
        var doc = XDocument.Parse(uncompressedXml);

        var diagramElem = doc.Descendants("diagram").FirstOrDefault();
        var extractedName = diagramName 
            ?? diagramNameFromRaw
            ?? (string?)diagramElem?.Attribute("name") 
            ?? "Quy trình nghiệp vụ (Draw.io)";

        // Thu thập tất cả các cell (bao gồm cả trường hợp bọc trong <object>)
        var root = doc.Descendants("root").FirstOrDefault() 
            ?? throw new InvalidOperationException("Không tìm thấy thẻ <root> trong mô hình Draw.io.");

        var rawNodes = new Dictionary<string, (string Label, string? Style, string? ParentId)>();
        var rawEdges = new List<DrawioEdge>();

        // Duyệt các element con trực tiếp của <root>
        foreach (var elem in root.Elements())
        {
            XElement cellElem;
            string? objectLabel = null;
            string id;

            if (elem.Name.LocalName.Equals("object", StringComparison.OrdinalIgnoreCase))
            {
                id = (string?)elem.Attribute("id") ?? string.Empty;
                objectLabel = (string?)elem.Attribute("label");
                cellElem = elem.Element("mxCell") ?? elem;
            }
            else if (elem.Name.LocalName.Equals("mxCell", StringComparison.OrdinalIgnoreCase))
            {
                id = (string?)elem.Attribute("id") ?? string.Empty;
                cellElem = elem;
            }
            else
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(id) || id == "0" || id == "1") continue;

            var isVertex = (string?)cellElem.Attribute("vertex") == "1";
            var isEdge = (string?)cellElem.Attribute("edge") == "1";
            var style = (string?)cellElem.Attribute("style") ?? string.Empty;
            var rawValue = objectLabel ?? (string?)cellElem.Attribute("value");
            var cleanLabel = CleanLabel(rawValue);
            var parentId = (string?)cellElem.Attribute("parent");

            if (isVertex)
            {
                // Bỏ qua các container rỗng không nhãn
                if (!string.IsNullOrWhiteSpace(cleanLabel) || style.Contains("swimlane", StringComparison.OrdinalIgnoreCase))
                {
                    rawNodes[id] = (cleanLabel, style, parentId);
                }
            }
            else if (isEdge)
            {
                var sourceId = (string?)cellElem.Attribute("source");
                var targetId = (string?)cellElem.Attribute("target");

                if (!string.IsNullOrWhiteSpace(sourceId) && !string.IsNullOrWhiteSpace(targetId))
                {
                    rawEdges.Add(new DrawioEdge(id, sourceId, targetId, cleanLabel));
                }
            }
        }

        // Tách Swimlanes (nếu có)
        var swimlanes = rawNodes
            .Where(kv => kv.Value.Style?.Contains("swimlane", StringComparison.OrdinalIgnoreCase) == true)
            .ToDictionary(kv => kv.Key, kv => kv.Value.Label);

        // Tạo danh sách Nodes chuẩn
        var nodes = new List<DrawioNode>();
        foreach (var (id, (label, style, parentId)) in rawNodes)
        {
            // Nếu chính nó là swimlane thì bỏ qua trong danh sách task hành động
            if (swimlanes.ContainsKey(id)) continue;

            string? lane = null;
            if (parentId != null && swimlanes.TryGetValue(parentId, out var parentLane))
            {
                lane = parentLane;
            }

            var shapeType = ClassifyShape(style, label);
            nodes.Add(new DrawioNode(id, label, shapeType, lane));
        }

        var validNodeIds = nodes.Select(n => n.Id).ToHashSet();
        var edges = rawEdges
            .Where(e => validNodeIds.Contains(e.SourceId) && validNodeIds.Contains(e.TargetId))
            .ToList();

        var paths = ExtractExecutionPaths(nodes, edges);
        var normalizedText = BuildNormalizedFlowText(extractedName, nodes, edges, paths);

        return new DrawioProcessDiagram
        {
            BusinessId = businessId,
            DiagramName = extractedName,
            Nodes = nodes,
            Edges = edges,
            ExecutionPaths = paths,
            NormalizedFlowText = normalizedText,
            ProcessedAt = DateTime.UtcNow
        };
    }

    private static string DecompressIfNeeded(string xml)
    {
        // Nếu đã có cấu trúc mxGraphModel trực tiếp
        if (xml.Contains("<mxGraphModel", StringComparison.OrdinalIgnoreCase))
            return xml;

        var startTag = "<diagram";
        var startIndex = xml.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0) return xml;

        var closeTagIndex = xml.IndexOf('>', startIndex);
        var endTagIndex = xml.IndexOf("</diagram>", closeTagIndex, StringComparison.OrdinalIgnoreCase);
        if (closeTagIndex < 0 || endTagIndex < 0) return xml;

        var payload = xml.Substring(closeTagIndex + 1, endTagIndex - closeTagIndex - 1).Trim();
        if (string.IsNullOrWhiteSpace(payload) || payload.Contains('<'))
            return xml;

        try
        {
            var compressedBytes = Convert.FromBase64String(payload);
            using var ms = new MemoryStream(compressedBytes);
            // Draw.io nén bằng raw deflate stream (RFC 1951)
            using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
            using var reader = new StreamReader(deflate, Encoding.UTF8);
            var decompressed = reader.ReadToEnd();
            var unescaped = Uri.UnescapeDataString(decompressed);
            if (unescaped.Contains("<mxGraphModel", StringComparison.OrdinalIgnoreCase))
                return unescaped;
        }
        catch
        {
            // Nếu giải nén thất bại, giữ nguyên XML ban đầu
        }

        return xml;
    }

    public static string CleanLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var text = raw;
        text = BrTagRegex().Replace(text, "\n");
        text = HtmlTagRegex().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = WhitespaceRegex().Replace(text, " ");
        return text.Trim();
    }

    private static string ClassifyShape(string? style, string label)
    {
        if (string.IsNullOrWhiteSpace(style)) return "Activity";

        if (style.Contains("rhombus", StringComparison.OrdinalIgnoreCase) ||
            style.Contains("diamond", StringComparison.OrdinalIgnoreCase) ||
            label.EndsWith('?'))
        {
            return "DecisionGateway";
        }

        if (style.Contains("ellipse", StringComparison.OrdinalIgnoreCase) ||
            style.Contains("circle", StringComparison.OrdinalIgnoreCase))
        {
            var lower = label.ToLower();
            if (lower.Contains("bắt đầu") || lower.Contains("start") || lower.Contains("begin"))
                return "StartEvent";
            if (lower.Contains("kết thúc") || lower.Contains("end") || lower.Contains("stop"))
                return "EndEvent";
            return "Event";
        }

        return "Activity";
    }

    private static List<string> ExtractExecutionPaths(List<DrawioNode> nodes, List<DrawioEdge> edges, int maxPaths = 20, int maxDepth = 15)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id);
        var outgoing = edges.GroupBy(e => e.SourceId).ToDictionary(g => g.Key, g => g.ToList());
        var incoming = edges.GroupBy(e => e.TargetId).ToDictionary(g => g.Key, g => g.ToList());

        // Tìm các Start Nodes: node có kiểu StartEvent hoặc không có cạnh đi vào
        var startNodes = nodes
            .Where(n => n.ShapeType == "StartEvent" || !incoming.ContainsKey(n.Id))
            .ToList();

        if (startNodes.Count == 0 && nodes.Count > 0)
        {
            startNodes.Add(nodes[0]);
        }

        var results = new List<string>();

        foreach (var start in startNodes)
        {
            DfsPath(start.Id, [start.Id], [""], outgoing, nodeMap, results, maxPaths, maxDepth);
            if (results.Count >= maxPaths) break;
        }

        return results;
    }

    private static void DfsPath(
        string currentId,
        List<string> currentPath,
        List<string> edgeLabels,
        Dictionary<string, List<DrawioEdge>> outgoing,
        Dictionary<string, DrawioNode> nodeMap,
        List<string> results,
        int maxPaths,
        int maxDepth)
    {
        if (results.Count >= maxPaths || currentPath.Count >= maxDepth) return;

        if (!outgoing.TryGetValue(currentId, out var edges) || edges.Count == 0)
        {
            // Đã đi tới điểm cuối của luồng
            if (currentPath.Count > 1)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < currentPath.Count; i++)
                {
                    var node = nodeMap[currentPath[i]];
                    var actorPrefix = string.IsNullOrWhiteSpace(node.Swimlane) ? "" : $"[{node.Swimlane}] ";
                    var stepText = $"{actorPrefix}{node.Label}";

                    if (i == 0)
                    {
                        sb.Append(stepText);
                    }
                    else
                    {
                        var edgeLabel = edgeLabels[i];
                        var cond = string.IsNullOrWhiteSpace(edgeLabel) ? "" : $" --(Nếu: {edgeLabel})--> ";
                        var arrow = string.IsNullOrWhiteSpace(cond) ? " --> " : cond;
                        sb.Append($"{arrow}{stepText}");
                    }
                }
                results.Add(sb.ToString());
            }
            return;
        }

        foreach (var edge in edges)
        {
            if (currentPath.Contains(edge.TargetId))
            {
                // Phát hiện vòng lặp (cycle) -> ghi nhận và dừng nhánh này
                var cycleNode = nodeMap.TryGetValue(edge.TargetId, out var n) ? n.Label : edge.TargetId;
                var edgeLabel = edge.Label ?? string.Empty;
                var cond = string.IsNullOrWhiteSpace(edgeLabel) ? "" : $" (Nếu: {edgeLabel})";
                results.Add(string.Join(" --> ", currentPath.Select(id => nodeMap[id].Label)) + $" --> [Vòng lặp lại: {cycleNode}{cond}]");
                continue;
            }

            if (!nodeMap.ContainsKey(edge.TargetId)) continue;

            currentPath.Add(edge.TargetId);
            edgeLabels.Add(edge.Label ?? string.Empty);

            DfsPath(edge.TargetId, currentPath, edgeLabels, outgoing, nodeMap, results, maxPaths, maxDepth);

            currentPath.RemoveAt(currentPath.Count - 1);
            edgeLabels.RemoveAt(edgeLabels.Count - 1);
        }
    }

    private static string BuildNormalizedFlowText(
        string diagramName,
        List<DrawioNode> nodes,
        List<DrawioEdge> edges,
        List<string> executionPaths)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### QUY TRÌNH NGHIỆP VỤ CHUẨN: {diagramName}");
        sb.AppendLine("Dưới đây là sơ đồ luồng nghiệp vụ chuẩn đã được giảng viên duyệt (Ground Truth):");
        sb.AppendLine();

        // 1. Danh sách các bước & hành động chính
        sb.AppendLine("1. DANH SÁCH CÁC BƯỚC VÀ THỰC THỂ (ACTORS / TASKS):");
        int stepIdx = 1;
        foreach (var node in nodes)
        {
            var lane = string.IsNullOrWhiteSpace(node.Swimlane) ? "" : $" (Phụ trách: {node.Swimlane})";
            var type = node.ShapeType switch
            {
                "DecisionGateway" => "[Cổng rẽ nhánh / Kiểm tra điều kiện]",
                "StartEvent" => "[Điểm bắt đầu]",
                "EndEvent" => "[Điểm kết thúc]",
                _ => "[Hành động]"
            };
            sb.AppendLine($"  - Bước {stepIdx++}: {type} \"{node.Label}\"{lane}");
        }
        sb.AppendLine();

        // 2. Các liên kết và điều kiện rẽ nhánh
        sb.AppendLine("2. LUỒNG CHUYỂN TIẾP VÀ ĐIỀU KIỆN (TRANSITIONS & GATEWAYS):");
        var nodeMap = nodes.ToDictionary(n => n.Id);
        foreach (var edge in edges)
        {
            if (nodeMap.TryGetValue(edge.SourceId, out var src) && nodeMap.TryGetValue(edge.TargetId, out var tgt))
            {
                var cond = string.IsNullOrWhiteSpace(edge.Label) ? "" : $" [Điều kiện: {edge.Label}]";
                sb.AppendLine($"  - \"{src.Label}\" --->{cond} Đến: \"{tgt.Label}\"");
            }
        }
        sb.AppendLine();

        // 3. Các kịch bản luồng thực thi hoàn chỉnh (Execution Paths)
        if (executionPaths.Count > 0)
        {
            sb.AppendLine("3. CÁC ĐƯỜNG ĐI THỰC THI CHUẨN (EXECUTION SCENARIOS):");
            for (int i = 0; i < executionPaths.Count; i++)
            {
                sb.AppendLine($"  - Luồng {i + 1}: {executionPaths[i]}");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
