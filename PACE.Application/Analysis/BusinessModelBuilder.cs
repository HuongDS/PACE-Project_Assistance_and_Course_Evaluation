using PACE.Application.Analysis.Common;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Application.Dtos;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Analysis;

public sealed record BusinessModel(
    List<MethodSource> Methods,
    List<Business> Businesses,
    List<BusinessMethod> BusinessMethods,
    List<BusinessFlow> Flows,
    List<DataFlowEdge> Edges);

/// <summary>Dựng nghiệp vụ, luồng gọi và sơ đồ Mermaid từ mã đã parse. Dùng cho phân tích repo và cho vấn đáp tại mốc hạn.</summary>
public static class BusinessModelBuilder
{
    public static BusinessModel Build(Guid runId, List<BusinessTemplate> templates, ParseResult parsed, AnalysisOptions settings, List<string> warnings)
    {
        var resolver = new CallResolver(parsed.Classes, parsed.Methods);
        var matcher = new EntryPointMatcher(parsed.Methods);
        var builder = new FlowBuilder(resolver, settings.MaxFlowDepth, settings.MaxStepsPerFlow);

        var frontendMethods = parsed.Methods
            .Where(m => m.ClassName.StartsWith(RepositoryScan.FrontendClassPrefix, StringComparison.Ordinal))
            .ToList();
        var methods = parsed.Methods.Select(m => new MethodSource
        {
            Id = m.Id,
            AnalysisRunId = runId,
            FilePath = Truncate(m.FilePath, 500),
            ClassName = Truncate(m.ClassName, 255)!,
            MethodName = Truncate(m.MethodName, 255)!,
            Parameters = m.Parameters.Count == 0 ? null : string.Join(", ", m.Parameters.Select(p => p.ToString())),
            SourceCode = m.SourceCode,
            Language = m.Language,
            NodeType = m.IsDecision ? NodeType.DecisionGateway : NodeType.Activity
        }).ToList();

        var businesses = new List<Business>();
        var businessMethods = new List<BusinessMethod>();
        var flows = new List<BusinessFlow>();
        var edges = new List<DataFlowEdge>();

        foreach (var template in templates)
        {
            var business = new Business
            {
                Id = Guid.NewGuid(),
                AnalysisRunId = runId,
                BusinessName = template.BusinessName
            };
            businesses.Add(business);

            var methodIds = new HashSet<Guid>();
            var entries = new HashSet<Guid>();

            foreach (var api in template.Apis)
            {
                var entry = matcher.Match(api, out var note);
                if (note is not null) warnings.Add($"[{template.BusinessName}] {note}");
                if (entry is null)
                {
                    warnings.Add($"[{template.BusinessName}] API {api.DisplayName} không khớp hàm nào trong code.");
                    continue;
                }
                if (!entries.Add(entry.Id)) continue;

                var graph = builder.Build(entry);
                if (graph.Truncated)
                    warnings.Add($"[{template.BusinessName}] Luồng {entry.Label} quá dài, đã cắt bớt (giới hạn {settings.MaxStepsPerFlow} bước / độ sâu {settings.MaxFlowDepth}).");

                var flow = new BusinessFlow
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    EntryMethodId = entry.Id,
                    Name = Truncate(api.DisplayName, 500)!,
                    MermaidGraph = MermaidBuilder.Build(graph)
                };
                flows.Add(flow);

                edges.AddRange(graph.Steps.Select(s => new DataFlowEdge
                {
                    Id = Guid.NewGuid(),
                    FlowId = flow.Id,
                    StepOrder = s.Order,
                    FromMethodId = s.From.Id,
                    ToMethodId = s.To.Id,
                    EdgeType = s.EdgeType,
                    ConditionContext = s.Condition,
                    DataLabel = s.DataLabel
                }));

                methodIds.UnionWith(graph.Methods.Select(m => m.Id));
                foreach (var screen in frontendMethods.Where(screen =>
                    (api.Path is { Length: > 1 } path && screen.SourceCode.Contains(path, StringComparison.Ordinal))
                    || (entry.ClassName.StartsWith(RepositoryScan.FrontendClassPrefix, StringComparison.Ordinal)
                        && string.Equals(Path.GetExtension(screen.FilePath), ".html", StringComparison.OrdinalIgnoreCase)
                        && screen.SourceCode.Contains(entry.FilePath.Split('/').Last(), StringComparison.Ordinal))))
                    methodIds.Add(screen.Id);
            }

            if (entries.Count == 0)
                warnings.Add($"[{template.BusinessName}] Nghiệp vụ không có code: không API nào khớp được hàm trong repo.");

            businessMethods.AddRange(methodIds.Select(id => new BusinessMethod { BusinessId = business.Id, MethodSourceId = id }));
        }

        return new BusinessModel(methods, businesses, businessMethods, flows, edges);
    }

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
