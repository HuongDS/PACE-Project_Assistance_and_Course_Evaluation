using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Models;

namespace PACE.Application.Analysis.Template;

public sealed record EvidenceNode(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("anchor")] string? Anchor = null,
    [property: JsonPropertyName("dfg_from")] IReadOnlyList<string>? DfgFrom = null);

public sealed record ContextGraphSummary(
    [property: JsonPropertyName("call_chain")] IReadOnlyList<string> CallChain,
    [property: JsonPropertyName("callee_summaries")] IReadOnlyDictionary<string, string> CalleeSummaries);

public sealed record EndpointEvidenceSlice(
    [property: JsonPropertyName("endpoint_id")] int EndpointId,
    [property: JsonPropertyName("controller")] string Controller,
    [property: JsonPropertyName("http_method")] string HttpMethod,
    [property: JsonPropertyName("function")] string Function,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("context_graph_summary")] ContextGraphSummary ContextGraphSummary,
    [property: JsonPropertyName("deobfuscated_evidence_slice")] IReadOnlyList<EvidenceNode> EvidenceNodes,
    [property: JsonPropertyName("code_anchors")] ApiCodeAnchors CodeAnchors,
    [property: JsonPropertyName("fallback_summary")] string FallbackSummary,
    [property: JsonPropertyName("fallback_behaviors")] IReadOnlyList<ApiBehaviorClaim> FallbackBehaviors);

/// <summary>
/// Cắt lát đồ thị thực thi (Forward &amp; Backward Slicing) và áp dụng Phương pháp A:
/// Total Alpha-Renaming / Name-Agnostic Deobfuscation (Alon et al., POPL 2019; Lacomis et al., USENIX Security 2019).
/// Ẩn danh hóa toàn bộ tên hàm/biến tự đặt thành fn_1, fn_2, VAR_1, VAR_2... và chỉ bảo toàn các Neo Bất Biến (DB/SDK/HTTP Status).
/// </summary>
public sealed class EndpointSubgraphSlicer(CallResolver resolver, int maxDepth = 6, int maxNodes = 25)
{
    public IReadOnlyList<EndpointEvidenceSlice> SliceAll(
        IReadOnlyList<TemplateEndpoint> endpoints,
        IReadOnlyCollection<ParsedMethod> allMethods)
    {
        var matcher = new EntryPointMatcher(allMethods);
        var results = new List<EndpointEvidenceSlice>(endpoints.Count);

        foreach (var ep in endpoints)
        {
            var api = ep.ToApi();
            var entry = matcher.Match(api, out _);
            results.Add(SliceOne(ep, entry));
        }

        return results;
    }

    public EndpointEvidenceSlice SliceOne(TemplateEndpoint ep, ParsedMethod? entry)
    {
        if (entry is null)
        {
            var emptyAnchors = new ApiCodeAnchors();
            var emptyContext = new ContextGraphSummary([], new Dictionary<string, string>());
            return new EndpointEvidenceSlice(
                ep.Id, ep.Controller, ep.HttpMethod, ep.Function, ep.Path,
                emptyContext, [], emptyAnchors,
                $"Xử lý yêu cầu {ep.HttpMethod} dựa trên luồng điều khiển nội bộ.",
                []);
        }

        // 1. Forward Slice (BFS/DFS up to maxDepth along CALLS)
        var subgraphMethods = new List<ParsedMethod>();
        var edges = new List<(ParsedMethod Caller, ParsedMethod Callee, string? Condition)>();
        var visited = new HashSet<Guid>();
        CollectForwardSlice(entry, 0, visited, subgraphMethods, edges);

        // 2. Collect all reachable Sinks & Invariants across subgraph
        var allSinks = subgraphMethods.SelectMany(m => m.Sinks).ToList();

        // 3. Total Alpha-Renaming (Name-Agnostic Deobfuscation):
        // Mọi hàm nội bộ của lập trình viên đều được đổi thành fn_1, fn_2...
        // Mọi tham số và biến cục bộ tự đặt đều được đổi thành VAR_1, VAR_2...
        var methodAliases = new Dictionary<Guid, string>();
        var varAliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var fnCounter = 1;
        var varCounter = 1;

        foreach (var m in subgraphMethods)
        {
            methodAliases[m.Id] = $"fn_{fnCounter++}";

            foreach (var p in m.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(p.Name) && !varAliases.ContainsKey(p.Name))
                    varAliases[p.Name] = $"VAR_{varCounter++}";
            }
            foreach (var df in m.DataFlows)
            {
                if (!string.IsNullOrWhiteSpace(df.Variable) && !varAliases.ContainsKey(df.Variable))
                    varAliases[df.Variable] = $"VAR_{varCounter++}";
            }
        }

        // 4. Build Evidence Nodes (Forward Sinks + Backward Branches/Returns + DFG)
        var evidenceNodes = new List<EvidenceNode>();
        var varDefNodeMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var nodeIdx = 1;

        string NextNodeId() => $"n{nodeIdx++}";

        // Entry parameters node (DATA)
        if (entry.Parameters.Count > 0)
        {
            var nId = NextNodeId();
            var paramDesc = string.Join(", ", entry.Parameters.Select(p =>
            {
                var pName = varAliases.GetValueOrDefault(p.Name, p.Name);
                return string.IsNullOrWhiteSpace(p.Type) ? pName : $"{p.Type} {pName}";
            }));
            evidenceNodes.Add(new EvidenceNode(nId, "ENTRY_DATA", methodAliases[entry.Id], $"Input: {paramDesc}"));
            foreach (var p in entry.Parameters)
                varDefNodeMap[p.Name] = nId;
        }

        foreach (var m in subgraphMethods)
        {
            if (evidenceNodes.Count >= maxNodes) break;
            var mLabel = methodAliases[m.Id];

            // Data Flow defs inside method
            foreach (var df in m.DataFlows.Take(4))
            {
                if (evidenceNodes.Count >= maxNodes) break;
                var vAlias = varAliases.GetValueOrDefault(df.Variable, df.Variable);
                var srcText = ApplyAliases(df.Source, varAliases, subgraphMethods, methodAliases);
                var dfgFrom = FindReferencedNodes(df.Source, varDefNodeMap);
                var nId = NextNodeId();
                varDefNodeMap[df.Variable] = nId;
                evidenceNodes.Add(new EvidenceNode(nId, "DATA_FLOW", mLabel, $"{vAlias} := {srcText}", null, dfgFrom));
            }

            // Sinks (DB_READ, DB_WRITE, SDK_CALL, HTTP_OUT, EVENT_PUB) - Bảo toàn 100% các Neo Bất Biến!
            foreach (var sink in m.Sinks)
            {
                if (evidenceNodes.Count >= maxNodes) break;
                var nId = NextNodeId();
                var kindStr = sink.Kind switch
                {
                    SinkKind.DbRead => "DB_READ",
                    SinkKind.DbWrite => "DB_WRITE",
                    SinkKind.SdkCall => "SDK_CALL",
                    SinkKind.HttpOut => "HTTP_OUT",
                    SinkKind.EventPub => "EVENT_PUB",
                    _ => "SINK"
                };
                var condPart = sink.Condition is not null
                    ? $" [when {ApplyAliases(sink.Condition, varAliases, subgraphMethods, methodAliases)}]"
                    : string.Empty;
                var detail = $"{kindStr} {sink.Target}{condPart}";
                var dfgFrom = FindReferencedNodes(sink.Detail ?? sink.Target, varDefNodeMap);
                evidenceNodes.Add(new EvidenceNode(nId, kindStr, mLabel, detail, sink.Target, dfgFrom));
            }

            // Branches / Guards (Backward slice conditions & error returns)
            foreach (var branch in m.Branches.Take(4))
            {
                if (evidenceNodes.Count >= maxNodes) break;
                var nId = NextNodeId();
                var cleanBranch = ApplyAliases(branch, varAliases, subgraphMethods, methodAliases);
                var dfgFrom = FindReferencedNodes(branch, varDefNodeMap);
                evidenceNodes.Add(new EvidenceNode(nId, "BRANCH", mLabel, cleanBranch, null, dfgFrom));
            }

            // Return / HTTP Status literals
            if (!string.IsNullOrWhiteSpace(m.ReturnExpression) || m.InvariantLiterals.Count > 0)
            {
                if (evidenceNodes.Count >= maxNodes) break;
                var nId = NextNodeId();
                var inv = m.InvariantLiterals.Count > 0 ? $" [Invariants: {string.Join(", ", m.InvariantLiterals)}]" : string.Empty;
                var retText = m.ReturnExpression is not null
                    ? ApplyAliases(m.ReturnExpression, varAliases, subgraphMethods, methodAliases)
                    : "void";
                var dfgFrom = m.ReturnExpression is not null ? FindReferencedNodes(m.ReturnExpression, varDefNodeMap) : null;
                evidenceNodes.Add(new EvidenceNode(nId, "RETURN", mLabel, $"return {retText}{inv}",
                    m.InvariantLiterals.FirstOrDefault(), dfgFrom));
            }
        }

        // Guarantee at least one evidence node for the entry method
        if (evidenceNodes.Count == 0)
        {
            evidenceNodes.Add(new EvidenceNode("n1", "ENTRY", methodAliases[entry.Id],
                $"Execute HTTP {ep.HttpMethod} handler ({methodAliases[entry.Id]})"));
        }

        // 5. Build Code Anchors (Invariant Anchors)
        var codeAnchors = new ApiCodeAnchors
        {
            DbReads = allSinks.Where(s => s.Kind == SinkKind.DbRead).Select(s => s.Target).Distinct().ToList(),
            DbWrites = allSinks.Where(s => s.Kind == SinkKind.DbWrite).Select(s => s.Target).Distinct().ToList(),
            ExternalSdks = allSinks.Where(s => s.Kind is SinkKind.SdkCall or SinkKind.HttpOut or SinkKind.EventPub)
                .Select(s => s.Target).Distinct().ToList()
        };

        // 6. Bottom-Up Topological Callee Summarization (McBurney & McMillan, ICPC 2014)
        var callChain = edges.Select(e => $"{methodAliases[e.Caller.Id]} -> {methodAliases[e.Callee.Id]}").Distinct().ToList();
        if (callChain.Count == 0) callChain.Add(methodAliases[entry.Id]);

        var calleeSummaries = BuildBottomUpCalleeSummaries(subgraphMethods, entry.Id, methodAliases);
        var contextGraph = new ContextGraphSummary(callChain, calleeSummaries);

        // 7. Build deterministic grounded fallback summary & behaviors (purely from anchors & evidence nodes)
        var (fallbackSummary, fallbackBehaviors) = BuildDeterministicFallback(ep, evidenceNodes, codeAnchors);

        return new EndpointEvidenceSlice(
            ep.Id,
            ep.Controller,
            ep.HttpMethod,
            ep.Function,
            ep.Path,
            contextGraph,
            evidenceNodes,
            codeAnchors,
            fallbackSummary,
            fallbackBehaviors);
    }

    private void CollectForwardSlice(
        ParsedMethod current,
        int depth,
        HashSet<Guid> visited,
        List<ParsedMethod> methods,
        List<(ParsedMethod Caller, ParsedMethod Callee, string? Condition)> edges)
    {
        if (!visited.Add(current.Id)) return;
        methods.Add(current);
        if (depth >= maxDepth) return;

        foreach (var call in current.Calls)
        {
            foreach (var target in resolver.Resolve(call, current))
            {
                edges.Add((current, target, call.Condition));
                if (!visited.Contains(target.Id))
                    CollectForwardSlice(target, depth + 1, visited, methods, edges);
            }
        }
    }

    private static Dictionary<string, string> BuildBottomUpCalleeSummaries(
        List<ParsedMethod> subgraphMethods,
        Guid entryId,
        Dictionary<Guid, string> methodAliases)
    {
        var summaries = new Dictionary<string, string>();
        for (var i = subgraphMethods.Count - 1; i >= 0; i--)
        {
            var m = subgraphMethods[i];
            if (m.Id == entryId && subgraphMethods.Count > 1) continue;

            var alias = methodAliases[m.Id];
            var parts = new List<string>();

            var reads = m.Sinks.Where(s => s.Kind == SinkKind.DbRead).Select(s => s.Target).Distinct().ToList();
            var writes = m.Sinks.Where(s => s.Kind == SinkKind.DbWrite).Select(s => s.Target).Distinct().ToList();
            var sdks = m.Sinks.Where(s => s.Kind is SinkKind.SdkCall or SinkKind.HttpOut or SinkKind.EventPub).Select(s => s.Target).Distinct().ToList();

            if (reads.Count > 0) parts.Add($"đọc DB [{string.Join(", ", reads)}]");
            if (writes.Count > 0) parts.Add($"ghi DB [{string.Join(", ", writes)}]");
            if (sdks.Count > 0) parts.Add($"gọi SDK/External [{string.Join(", ", sdks)}]");
            if (m.Branches.Count > 0) parts.Add($"kiểm tra {m.Branches.Count} điều kiện rẽ nhánh");
            if (m.InvariantLiterals.Count > 0) parts.Add($"trạng thái [{string.Join(", ", m.InvariantLiterals)}]");

            if (parts.Count == 0)
                parts.Add("xử lý biến đổi dữ liệu nội bộ và trả kết quả");

            summaries[alias] = string.Join("; ", parts);
        }

        return summaries;
    }

    private static (string Summary, List<ApiBehaviorClaim> Behaviors) BuildDeterministicFallback(
        TemplateEndpoint ep,
        List<EvidenceNode> nodes,
        ApiCodeAnchors anchors)
    {
        var behaviors = new List<ApiBehaviorClaim>();
        var claimIdx = 1;

        var readNodes = nodes.Where(n => n.Kind == "DB_READ").ToList();
        if (readNodes.Count > 0)
        {
            behaviors.Add(new ApiBehaviorClaim
            {
                ClaimId = $"C{claimIdx++}",
                Action = $"Truy vấn dữ liệu từ: {string.Join(", ", anchors.DbReads)}.",
                EvidenceNodes = readNodes.Select(n => n.Id).ToList(),
                Confidence = 0.95
            });
        }

        var branchNodes = nodes.Where(n => n.Kind == "BRANCH").ToList();
        if (branchNodes.Count > 0)
        {
            behaviors.Add(new ApiBehaviorClaim
            {
                ClaimId = $"C{claimIdx++}",
                Action = $"Kiểm tra điều kiện hợp lệ và rẽ nhánh xử lý ({branchNodes.Count} điều kiện).",
                EvidenceNodes = branchNodes.Select(n => n.Id).ToList(),
                Confidence = 0.92
            });
        }

        var writeNodes = nodes.Where(n => n.Kind == "DB_WRITE").ToList();
        if (writeNodes.Count > 0)
        {
            behaviors.Add(new ApiBehaviorClaim
            {
                ClaimId = $"C{claimIdx++}",
                Action = $"Cập nhật/ghi dữ liệu vào: {string.Join(", ", anchors.DbWrites)}.",
                EvidenceNodes = writeNodes.Select(n => n.Id).ToList(),
                Confidence = 0.96
            });
        }

        var sdkNodes = nodes.Where(n => n.Kind is "SDK_CALL" or "HTTP_OUT" or "EVENT_PUB").ToList();
        if (sdkNodes.Count > 0)
        {
            behaviors.Add(new ApiBehaviorClaim
            {
                ClaimId = $"C{claimIdx++}",
                Action = $"Tương tác dịch vụ/SDK: {string.Join(", ", anchors.ExternalSdks)}.",
                EvidenceNodes = sdkNodes.Select(n => n.Id).ToList(),
                Confidence = 0.95
            });
        }

        if (behaviors.Count == 0)
        {
            behaviors.Add(new ApiBehaviorClaim
            {
                ClaimId = "C1",
                Action = $"Thực thi luồng xử lý HTTP {ep.HttpMethod} dựa trên đồ thị dữ liệu nội bộ.",
                EvidenceNodes = nodes.Take(2).Select(n => n.Id).ToList(),
                Confidence = 0.85
            });
        }

        var summaryParts = new List<string>();
        if (anchors.DbReads.Count > 0) summaryParts.Add($"tra cứu {string.Join(", ", anchors.DbReads)}");
        if (anchors.DbWrites.Count > 0) summaryParts.Add($"cập nhật {string.Join(", ", anchors.DbWrites)}");
        if (anchors.ExternalSdks.Count > 0) summaryParts.Add($"gọi {string.Join(", ", anchors.ExternalSdks)}");

        string summary;
        if (summaryParts.Count > 0)
        {
            var joined = string.Join(", ", summaryParts);
            summary = char.ToUpperInvariant(joined[0]) + joined[1..] + ".";
        }
        else
        {
            summary = $"Xử lý yêu cầu HTTP {ep.HttpMethod} dựa trên luồng điều khiển và dữ liệu nội bộ.";
        }

        return (summary, behaviors);
    }

    private static string ApplyAliases(
        string expression,
        Dictionary<string, string> varAliases,
        List<ParsedMethod> subgraphMethods,
        Dictionary<Guid, string> methodAliases)
    {
        if (string.IsNullOrEmpty(expression)) return expression;
        var result = expression;
        foreach (var (orig, alias) in varAliases)
        {
            result = Regex.Replace(result, $@"\b{Regex.Escape(orig)}\b", alias);
        }
        foreach (var m in subgraphMethods)
        {
            if (methodAliases.TryGetValue(m.Id, out var fnAlias))
                result = Regex.Replace(result, $@"\b{Regex.Escape(m.MethodName)}\b", fnAlias);
        }
        return result;
    }

    private static IReadOnlyList<string>? FindReferencedNodes(string expression, Dictionary<string, string> varDefNodeMap)
    {
        if (varDefNodeMap.Count == 0 || string.IsNullOrEmpty(expression)) return null;
        var refs = new List<string>();
        foreach (var (varName, nodeId) in varDefNodeMap)
        {
            if (Regex.IsMatch(expression, $@"\b{Regex.Escape(varName)}\b") && !refs.Contains(nodeId))
                refs.Add(nodeId);
        }
        return refs.Count > 0 ? refs : null;
    }
}
