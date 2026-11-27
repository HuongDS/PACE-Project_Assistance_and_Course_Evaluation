using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PACE.Application.Analysis.Models;

namespace PACE.Application.Analysis.Template;

public sealed record TemplateEndpoint(int Id, string Controller, string HttpMethod, string Function, string? Path)
{
    public ApiTemplate ToApi(EndpointEvidenceSlice? slice = null, SynthesizedEndpointSpec? synthesized = null)
    {
        if (slice is null && synthesized is null)
        {
            return new ApiTemplate
            {
                Controller = Controller,
                HttpMethod = HttpMethod,
                Function = Function,
                Path = Path
            };
        }

        var validNodeIds = slice?.EvidenceNodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        List<ApiBehaviorClaim>? behaviors = null;

        if (synthesized?.Behaviors is { Count: > 0 })
        {
            behaviors = synthesized.Behaviors
                .Where(b => !string.IsNullOrWhiteSpace(b.Action))
                .Select((b, idx) =>
                {
                    var ev = (b.EvidenceNodes ?? [])
                        .Where(id => validNodeIds.Count == 0 || validNodeIds.Contains(id))
                        .Distinct()
                        .ToList();
                    if (ev.Count == 0 && slice is { EvidenceNodes.Count: > 0 })
                        ev.Add(slice.EvidenceNodes[0].Id);

                    return new ApiBehaviorClaim
                    {
                        ClaimId = string.IsNullOrWhiteSpace(b.ClaimId) ? $"C{idx + 1}" : b.ClaimId.Trim(),
                        Action = b.Action.Trim(),
                        EvidenceNodes = ev,
                        Confidence = Math.Clamp(b.Confidence <= 0 ? 0.95 : b.Confidence, 0.0, 1.0)
                    };
                })
                .ToList();
        }

        if ((behaviors is null || behaviors.Count == 0) && slice is { FallbackBehaviors.Count: > 0 })
        {
            behaviors = slice.FallbackBehaviors.ToList();
        }

        var summary = !string.IsNullOrWhiteSpace(synthesized?.Summary)
            ? synthesized.Summary.Trim()
            : slice?.FallbackSummary;

        return new ApiTemplate
        {
            Controller = Controller,
            HttpMethod = HttpMethod,
            Function = Function,
            Path = Path,
            Summary = summary,
            Behaviors = behaviors,
            CodeAnchors = slice?.CodeAnchors
        };
    }
}

public sealed record SynthesizedEndpointSpec(
    int EndpointId,
    string? Summary,
    IReadOnlyList<ApiBehaviorClaim>? Behaviors);

public sealed record SuggestedBusiness(string BusinessName, string? Description, IReadOnlyList<int> EndpointIds);

public sealed record LlmTokenUsage(int InputTokens = 0, int OutputTokens = 0, int TotalTokens = 0, int ApiCalls = 0)
{
    public static LlmTokenUsage Empty { get; } = new(0, 0, 0, 0);

    public LlmTokenUsage Add(LlmTokenUsage other)
        => new(
            InputTokens + other.InputTokens,
            OutputTokens + other.OutputTokens,
            TotalTokens + other.TotalTokens,
            ApiCalls + other.ApiCalls);
}

public sealed record LegacyGroupingResult(
    IReadOnlyList<SuggestedBusiness> Groups,
    LlmTokenUsage TokenUsage);

public sealed record HybridSynthesisResult(
    IReadOnlyList<SuggestedBusiness> Groups,
    IReadOnlyDictionary<int, SynthesizedEndpointSpec> EndpointSpecs,
    LlmTokenUsage? TokenUsage = null,
    IReadOnlyList<string>? Warnings = null);

public static partial class BusinessTemplateGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex CamelBoundary();

    public static List<TemplateEndpoint> ExtractEndpoints(IEnumerable<ParsedMethod> methods)
        => methods
            .Where(m => m.HttpVerb is not null && !m.IsAbstract)
            .OrderBy(m => m.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.FilePath, StringComparer.Ordinal)
            .ThenBy(m => m.StartLine)
            .DistinctBy(m => (m.ClassName.ToLowerInvariant(), m.HttpVerb!.ToUpperInvariant(), m.MethodName, m.Route))
            .Select((m, i) => new TemplateEndpoint(i, m.ClassName, m.HttpVerb!.ToUpperInvariant(), m.MethodName, ExpandRouteTokens(m)))
            .ToList();

    /// <summary>Thay token [controller]/[action] của ASP.NET bằng tên thật để path đọc được.</summary>
    private static string? ExpandRouteTokens(ParsedMethod m)
    {
        if (m.Route is null) return null;
        var controller = m.ClassName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) && m.ClassName.Length > "Controller".Length
            ? m.ClassName[..^"Controller".Length]
            : m.ClassName;
        return m.Route
            .Replace("[controller]", controller.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
            .Replace("[action]", m.MethodName.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
    }

    public static List<BusinessTemplate> GroupByController(
        IEnumerable<TemplateEndpoint> endpoints,
        IReadOnlyDictionary<int, EndpointEvidenceSlice>? slicesById = null,
        IReadOnlyDictionary<int, SynthesizedEndpointSpec>? specsById = null)
        => endpoints
            .GroupBy(e => e.Controller, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var apis = g.Select(e =>
                {
                    var slice = slicesById?.GetValueOrDefault(e.Id);
                    var spec = specsById?.GetValueOrDefault(e.Id);
                    return e.ToApi(slice, spec);
                }).ToList();
                return new BusinessTemplate
                {
                    BusinessName = DeriveBusinessName(g.Key),
                    Description = $"Gom theo controller {g.Key} ({apis.Count} API).",
                    Apis = apis
                };
            })
            .ToList();

    /// <summary>
    /// Áp gợi ý gom nhóm (từ AI) lên danh sách endpoint đã quét. Chỉ dùng id có thật, mỗi endpoint thuộc đúng một
    /// nghiệp vụ; endpoint bị bỏ sót được gom lại theo controller để không mất API nào.
    /// </summary>
    public static List<BusinessTemplate> ApplyGrouping(
        IReadOnlyList<TemplateEndpoint> endpoints,
        IEnumerable<SuggestedBusiness> groups,
        List<string> warnings,
        IReadOnlyDictionary<int, EndpointEvidenceSlice>? slicesById = null,
        IReadOnlyDictionary<int, SynthesizedEndpointSpec>? specsById = null,
        bool groupMissing = true)
    {
        var byId = endpoints.ToDictionary(e => e.Id);
        var assigned = new HashSet<int>();
        var unknown = 0;
        var result = new List<BusinessTemplate>();

        foreach (var group in groups)
        {
            if (string.IsNullOrWhiteSpace(group.BusinessName)) continue;
            var apis = new List<ApiTemplate>();
            foreach (var id in group.EndpointIds ?? [])
            {
                if (!byId.TryGetValue(id, out var endpoint)) { unknown++; continue; }
                if (assigned.Add(id))
                {
                    var slice = slicesById?.GetValueOrDefault(id);
                    var spec = specsById?.GetValueOrDefault(id);
                    apis.Add(endpoint.ToApi(slice, spec));
                }
            }
            if (apis.Count == 0) continue;
            result.Add(new BusinessTemplate
            {
                BusinessName = group.BusinessName.Trim(),
                Description = string.IsNullOrWhiteSpace(group.Description) ? null : group.Description.Trim(),
                Apis = apis
            });
        }

        if (unknown > 0) warnings.Add($"AI trả về {unknown} API không có trong repo, đã bỏ qua.");

        var missing = endpoints.Where(e => !assigned.Contains(e.Id)).ToList();
        if (missing.Count > 0)
        {
            const int shown = 10;
            var names = string.Join("; ", missing.Take(shown).Select(e =>
                $"{e.HttpMethod} {(string.IsNullOrWhiteSpace(e.Path) ? e.Function : e.Path)} ({e.Controller}.{e.Function})"));
            if (missing.Count > shown) names += $"; và {missing.Count - shown} API khác";

            if (groupMissing)
            {
                warnings.Add($"AI bỏ sót {missing.Count} API, đã gom các API này theo controller: {names}.");
                result.AddRange(GroupByController(missing, slicesById, specsById));
            }
            else
            {
                warnings.Add($"AI chưa xếp {missing.Count} API vào nghiệp vụ nào: {names}. " +
                             "Các API này nằm ở mục \"API chưa thuộc nghiệp vụ nào\", nhóm chọn nghiệp vụ phù hợp cho từng API.");
            }
        }

        return result;
    }

    public static string DeriveBusinessName(string controller)
    {
        var name = controller.Trim();
        if (name.Length > "Controller".Length && name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            name = name[..^"Controller".Length];
        name = name.Replace('_', ' ').Replace('-', ' ');
        return string.Join(' ', CamelBoundary().Split(name).SelectMany(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    public static string Serialize(IReadOnlyList<BusinessTemplate> businesses)
        => JsonSerializer.Serialize(businesses, JsonOptions);
}
