using System.Text.Json;
using TreeSitter;
using PACE.Application.Analysis.Models;

namespace PACE.Infrastructure.Analysis.Parsing;

internal sealed record SpringMapping(IReadOnlyList<string> Paths, IReadOnlyList<string> Verbs);
internal sealed record SpringMethodMapping(bool IsController, SpringMapping? Type, SpringMapping? Method);

internal sealed class JavaSpringMappings(Node root)
{
    private const string WebAnnotations = "org.springframework.web.bind.annotation.";
    private const string RequestMethod = WebAnnotations + "RequestMethod";
    private const string Controller = "org.springframework.stereotype.Controller";
    private const int MaxMappingsPerHandler = 4096;
    private static readonly Dictionary<string, string> Shortcuts = new(StringComparer.Ordinal)
    {
        ["GetMapping"] = "GET", ["PostMapping"] = "POST", ["PutMapping"] = "PUT",
        ["DeleteMapping"] = "DELETE", ["PatchMapping"] = "PATCH"
    };
    private static readonly HashSet<string> RequestVerbs = ["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "TRACE"];

    private readonly HashSet<string> _imports = root.NamedChildren.Where(node => node.Type == "import_declaration")
        .Select(node => node.Text["import".Length..].Trim().TrimEnd(';').Trim()).ToHashSet(StringComparer.Ordinal);
    private readonly HashSet<string> _localAnnotations = root.NamedChildren.Where(node => node.Type == "annotation_type_declaration")
        .Select(node => node.FieldText("name") ?? "").ToHashSet(StringComparer.Ordinal);

    public bool IsController(Node? modifiers) => Annotations(modifiers).Any(annotation =>
        IsType(annotation.FieldText("name"), WebAnnotations + "RestController") ||
        IsType(annotation.FieldText("name"), Controller));

    public SpringMapping? Read(Node? modifiers)
    {
        foreach (var annotation in Annotations(modifiers))
        {
            var type = annotation.FieldText("name");
            var shortcut = Shortcuts.FirstOrDefault(pair => IsType(type, WebAnnotations + pair.Key));
            var isRequestMapping = IsType(type, WebAnnotations + "RequestMapping");
            if (!isRequestMapping && shortcut.Key is null) continue;

            var args = annotation.Field("arguments")?.NamedChildren ?? [];
            var aliases = new List<IReadOnlyList<string>>();
            IReadOnlyList<string> verbs = isRequestMapping ? [] : [shortcut.Value];
            foreach (var arg in args.Where(node => node.Type != "line_comment" && node.Type != "block_comment"))
            {
                if (arg.Type != "element_value_pair")
                {
                    aliases.Add(Paths(arg));
                    continue;
                }
                var key = arg.FieldText("key");
                var value = arg.Field("value");
                if (key is "value" or "path")
                    aliases.Add(value is null ? [] : Paths(value));
                else if (key == "method" && isRequestMapping && value is not null)
                {
                    var items = Elements(value).ToArray();
                    verbs = items.Select(Verb).OfType<string>().Distinct().ToArray();
                    if (items.Length > 0 && verbs.Count == 0) return new SpringMapping([], []);
                }
            }

            if (aliases.Count == 0) return new SpringMapping([""], verbs);
            var paths = aliases[0];
            if (aliases.Skip(1).Any(other => !other.ToHashSet(StringComparer.Ordinal).SetEquals(paths)))
                return new SpringMapping([], verbs);
            return new SpringMapping(paths, verbs);
        }

        return null;
    }

    public static void Apply(IEnumerable<JavaSourcePositions> sources, ParseResult result)
    {
        var methods = sources.SelectMany(source => source.SpringMethods).ToArray();
        var bySymbol = methods.Where(pair => pair.Key.JavaSymbol is not null && pair.Value.Method is not null)
            .ToLookup(pair => pair.Key.JavaSymbol!, pair => pair.Value, StringComparer.Ordinal);
        var omitted = 0;
        foreach (var (method, metadata) in methods)
        {
            if (!metadata.IsController || method.IsAbstract) continue;
            if (metadata.Method is { } declared)
            {
                method.SpringEndpoints.AddRange(Combine(metadata.Type, declared).Distinct());
                if (method.SpringEndpoints.Count == 0) omitted++;
                continue;
            }

            var inherited = method.JavaOverriddenSymbols
                .SelectMany(symbol => bySymbol[symbol])
                .Select(parent => Combine(metadata.Type ?? parent.Type, parent.Method!).ToHashSet())
                .ToArray();
            if (inherited.Length == 0) continue;
            if (inherited[0].Count == 0 || inherited.Skip(1).Any(other => !other.SetEquals(inherited[0])))
            {
                omitted++;
                continue;
            }
            method.SpringEndpoints.AddRange(inherited[0].OrderBy(endpoint => endpoint.Path, StringComparer.Ordinal)
                .ThenBy(endpoint => endpoint.Verb, StringComparer.Ordinal));
        }
        if (omitted > 0)
            result.Warnings.Add($"Spring: bỏ qua {omitted} handler có mapping không xác định, xung đột hoặc vượt giới hạn {MaxMappingsPerHandler} tổ hợp.");
    }

    private static IEnumerable<EndpointMapping> Combine(SpringMapping? type, SpringMapping method)
    {
        var verbs = (type?.Verbs ?? []).Concat(method.Verbs).Distinct().DefaultIfEmpty("ANY").ToArray();
        var prefixes = type?.Paths ?? [""];
        if ((long)prefixes.Count * method.Paths.Count * verbs.Length > MaxMappingsPerHandler) return [];
        return from prefix in prefixes
               from path in method.Paths
               from verb in verbs
               select new EndpointMapping(verb, Join(prefix, path));
    }

    private static string Join(string prefix, string path)
    {
        if (path.Length == 0) return "/" + prefix.TrimStart('/');
        if (prefix.Trim('/').Length == 0) return "/" + path.TrimStart('/');
        return "/" + prefix.Trim('/') + "/" + path.TrimStart('/');
    }

    private static IEnumerable<Node> Annotations(Node? modifiers)
        => modifiers?.NamedChildren.Where(node => node.Type is "annotation" or "marker_annotation") ?? [];

    private static IEnumerable<Node> Elements(Node value)
        => value.Type != "element_value_array_initializer" ? [value]
            : value.NamedChildren.Where(node => node.Type is not ("line_comment" or "block_comment"));

    private static string[] Paths(Node value)
    {
        var elements = Elements(value).ToArray();
        if (elements.Length == 0) return [""];
        return elements.Where(node => node.Type == "string_literal")
            .Select(node => ReadPath(node.Text)).OfType<string>()
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string? ReadPath(string literal)
    {
        if (literal.Length < 2 || literal[^1] != '"' || literal.StartsWith("\"\"\"", StringComparison.Ordinal)) return null;
        try
        {
            // Ordinary Java escapes used in route regexes (e.g. "\\d") are also valid JSON escapes.
            var path = literal.Contains('\\') ? JsonSerializer.Deserialize<string>(literal) : literal[1..^1];
            return path is null || path.Any(char.IsControl) || path.Contains("${") || path.Contains("#{") ? null : path;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string? Verb(Node node)
    {
        var text = node.Text;
        var dot = text.LastIndexOf('.');
        if (dot >= 0)
        {
            var verb = text[(dot + 1)..];
            return RequestVerbs.Contains(verb) && IsType(text[..dot], RequestMethod) ? verb : null;
        }
        return RequestVerbs.Contains(text) &&
            (_imports.Contains($"static {RequestMethod}.{text}") || _imports.Contains($"static {RequestMethod}.*")) ? text : null;
    }

    private bool IsType(string? reference, string qualifiedName)
    {
        if (reference == qualifiedName) return true;
        if (reference is null || reference.Contains('.') || _localAnnotations.Contains(reference)) return false;
        var separator = qualifiedName.LastIndexOf('.');
        if (reference != qualifiedName[(separator + 1)..]) return false;
        if (_imports.Contains(qualifiedName)) return true;
        return !_imports.Any(import => import.EndsWith("." + reference, StringComparison.Ordinal)) &&
            _imports.Contains(qualifiedName[..(separator + 1)] + "*");
    }
}
