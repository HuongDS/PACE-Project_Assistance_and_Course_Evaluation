using System.Text.Json.Serialization;
using System.Text.Json;
using PACE.Application.Common.Exceptions;

namespace PACE.Application.Analysis.Template;

public sealed class BusinessTemplate
{
    [JsonPropertyName("business_name")]
    public string BusinessName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("apis")]
    public List<ApiTemplate> Apis { get; set; } = [];
}

public sealed class ApiTemplate
{
    [JsonPropertyName("controller")]
    public string Controller { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("http_method")]
    public string? HttpMethod { get; set; }

    [JsonPropertyName("function")]
    public string? Function { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    public string ResolveFunctionName()
    {
        if (!string.IsNullOrWhiteSpace(Function)) return Function.Trim();
        if (string.IsNullOrWhiteSpace(Method)) return string.Empty;
        var parts = Method.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : parts[0];
    }

    public string? ResolveHttpVerb()
    {
        if (!string.IsNullOrWhiteSpace(HttpMethod)) return HttpMethod.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(Method)) return null;
        var parts = Method.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[0].ToUpperInvariant() : null;
    }

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var verb = ResolveHttpVerb();
            var name = ResolveFunctionName();
            return verb is null ? $"{Controller}.{name}" : $"{verb} {Controller}.{name}";
        }
    }
}

public static class BusinessTemplateLoader
{
    public const string FileName = "template_business.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static List<BusinessTemplate> Parse(string json)
    {
        List<BusinessTemplate>? businesses;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                var array = root.EnumerateObject().FirstOrDefault(p => p.Value.ValueKind == JsonValueKind.Array);
                if (array.Value.ValueKind != JsonValueKind.Array)
                    throw new BadRequestException("template_business.json phải là một mảng các nghiệp vụ hoặc object chứa mảng \"businesses\".");
                root = array.Value;
            }

            businesses = root.Deserialize<List<BusinessTemplate>>(Options);
        }
        catch (JsonException ex)
        {
            throw new BadRequestException($"template_business.json sai định dạng JSON: {ex.Message}", ex);
        }

        return Normalize(businesses);
    }

    /// <summary>Bỏ nghiệp vụ/API thiếu thông tin và gộp nghiệp vụ trùng tên (không phân biệt hoa thường).</summary>
    public static List<BusinessTemplate> Normalize(IEnumerable<BusinessTemplate?>? source)
    {
        var businesses = source?
            .Where(b => b is not null && !string.IsNullOrWhiteSpace(b.BusinessName))
            .Select(b =>
            {
                b!.BusinessName = b.BusinessName.Trim();
                b.Apis = (b.Apis ?? []).Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Controller) && a.ResolveFunctionName().Length > 0).ToList();
                return b;
            })
            .ToList();

        if (businesses is null || businesses.Count == 0)
            throw new BadRequestException("template_business.json không có nghiệp vụ nào (cần business_name và apis).");

        return businesses
            .GroupBy(b => b.BusinessName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                first.Apis = g.SelectMany(b => b.Apis).ToList();
                first.Description ??= g.Select(b => b.Description).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
                return first;
            })
            .ToList();
    }
}
