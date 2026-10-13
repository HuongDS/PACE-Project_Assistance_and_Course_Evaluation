using System.Text.RegularExpressions;

namespace PACE.BLL.Analysis.Parsing;

public static partial class ParserText
{
    public const int MaxSourceLength = 100_000;
    public const int MaxConditionLength = 80;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static string OneLine(string text, int maxLength = MaxConditionLength)
    {
        var flat = Whitespace().Replace(text, " ").Trim();
        return flat.Length <= maxLength ? flat : flat[..(maxLength - 3)] + "...";
    }

    public static string StripParens(string text)
    {
        var t = text.Trim();
        while (t.Length >= 2 && t[0] == '(' && t[^1] == ')' && IsWrapped(t)) t = t[1..^1].Trim();
        return t;
    }

    private static bool IsWrapped(string t)
    {
        var depth = 0;
        for (var i = 0; i < t.Length; i++)
        {
            if (t[i] == '(') depth++;
            else if (t[i] == ')') depth--;
            if (depth == 0 && i < t.Length - 1) return false;
        }
        return depth == 0;
    }

    public static string Source(string text)
        => text.Length <= MaxSourceLength ? text : text[..MaxSourceLength] + "\n/* ... truncated ... */";

    public static string SimpleTypeName(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return string.Empty;
        var t = type.Trim();
        var generic = t.IndexOf('<');
        if (generic > 0) t = t[..generic];
        t = t.TrimEnd('?', ']', '[').Trim();
        var dot = t.LastIndexOf('.');
        if (dot >= 0 && dot < t.Length - 1) t = t[(dot + 1)..];
        return t;
    }

    public static string PascalCase(string name)
    {
        var parts = name.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    public static string? StringLiteralValue(string? literal)
    {
        if (string.IsNullOrWhiteSpace(literal)) return null;
        var t = literal.Trim();
        if (t.Length >= 2 && (t[0] is '"' or '\'' or '`') && t[^1] == t[0]) return t[1..^1];
        return null;
    }

    public static string CombineRoute(string? prefix, string? route)
    {
        var a = (prefix ?? string.Empty).Trim().Trim('/');
        var b = (route ?? string.Empty).Trim().Trim('/');
        if (a.Length == 0) return "/" + b;
        if (b.Length == 0) return "/" + a;
        return $"/{a}/{b}";
    }
}
