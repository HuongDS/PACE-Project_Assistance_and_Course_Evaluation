using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PACE.Application.Analysis.Models;

namespace PACE.Application.Analysis.Template;

public sealed record TemplateEndpoint(int Id, string Controller, string HttpMethod, string Function, string? Path)
{
    public ApiTemplate ToApi() => new() { Controller = Controller, HttpMethod = HttpMethod, Function = Function, Path = Path };
}

public sealed record SuggestedBusiness(string BusinessName, string? Description, IReadOnlyList<int> EndpointIds);

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

    public static List<BusinessTemplate> GroupByController(IEnumerable<TemplateEndpoint> endpoints)
        => endpoints
            .GroupBy(e => e.Controller, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var apis = g.Select(e => e.ToApi()).ToList();
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
        IReadOnlyList<TemplateEndpoint> endpoints, IEnumerable<SuggestedBusiness> groups, List<string> warnings)
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
                if (assigned.Add(id)) apis.Add(endpoint.ToApi());
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
            warnings.Add($"AI bỏ sót {missing.Count} API, đã gom các API này theo controller.");
            result.AddRange(GroupByController(missing));
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
