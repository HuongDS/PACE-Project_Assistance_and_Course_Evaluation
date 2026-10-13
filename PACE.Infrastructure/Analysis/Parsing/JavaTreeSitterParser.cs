using TreeSitter;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Common;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Analysis.Parsing;

public sealed class JavaTreeSitterParser : ILanguageParser
{
    private static readonly HashSet<string> TypeDeclarations = ["class_declaration", "interface_declaration", "enum_declaration", "record_declaration"];

    private static readonly HashSet<string> BranchNodes =
    [
        "if_statement", "switch_expression", "switch_statement", "ternary_expression", "while_statement",
        "for_statement", "enhanced_for_statement", "do_statement"
    ];

    private static readonly Dictionary<string, string> MappingAnnotations = new(StringComparer.Ordinal)
    {
        ["GetMapping"] = "GET",
        ["PostMapping"] = "POST",
        ["PutMapping"] = "PUT",
        ["DeleteMapping"] = "DELETE",
        ["PatchMapping"] = "PATCH",
        ["RequestMapping"] = "ANY"
    };

    public string Name => "Java (tree-sitter)";

    public SourceKind Kind => SourceKind.Java;

    public ParseResult Parse(IReadOnlyList<SourceFile> files, CancellationToken ct)
    {
        var result = new ParseResult();
        if (files.Count == 0) return result;

        using var language = new Language("Java");
        using var parser = new Parser(language);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var tree = parser.Parse(file.Content);
                if (tree is null)
                {
                    result.Warnings.Add($"Không parse được Java {file.RelativePath}");
                    continue;
                }

                foreach (var type in tree.RootNode.Descendants().Where(n => TypeDeclarations.Contains(n.Type)))
                    ParseType(type, file.RelativePath, result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.Warnings.Add($"Lỗi parse Java {file.RelativePath}: {ex.Message}");
            }
        }

        return result;
    }

    private static void ParseType(Node type, string filePath, ParseResult result)
    {
        var className = type.FieldText("name");
        if (string.IsNullOrEmpty(className)) return;

        var modifiers = type.NamedChildren.FirstOrDefault(c => c.Type == "modifiers");
        var parsedClass = new ParsedClass
        {
            Name = className,
            Kind = type.Type.Replace("_declaration", string.Empty),
            FilePath = filePath,
            Language = ProgrammingLanguage.Java,
            RoutePrefix = modifiers is null ? null : FindMapping(modifiers).Route
        };

        var superclass = type.Field("superclass");
        if (superclass is not null) parsedClass.BaseTypes.AddRange(TypeNames(superclass));
        var interfaces = type.Field("interfaces") ?? type.NamedChildren.FirstOrDefault(c => c.Type is "super_interfaces" or "extends_interfaces");
        if (interfaces is not null) parsedClass.BaseTypes.AddRange(TypeNames(interfaces));
        result.Classes.Add(parsedClass);

        var body = type.Field("body");
        if (body is null) return;

        var members = body.NamedChildren.ToList();
        var enumDeclarations = members.FirstOrDefault(m => m.Type == "enum_body_declarations");
        if (enumDeclarations is not null) members.AddRange(enumDeclarations.NamedChildren);

        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in members.Where(m => m.Type == "field_declaration"))
        {
            var fieldType = ParserText.SimpleTypeName(field.FieldText("type"));
            foreach (var declarator in field.NamedChildren.Where(c => c.Type == "variable_declarator"))
            {
                var name = declarator.FieldText("name");
                if (!string.IsNullOrEmpty(name)) fields[name] = fieldType;
            }
        }

        if (type.Type == "record_declaration" && type.Field("parameters") is { } recordParams)
        {
            foreach (var p in recordParams.NamedChildren)
            {
                var name = p.FieldText("name");
                if (!string.IsNullOrEmpty(name)) fields[name] = ParserText.SimpleTypeName(p.FieldText("type"));
            }
        }

        var superName = parsedClass.BaseTypes.FirstOrDefault();
        foreach (var method in members.Where(m => m.Type == "method_declaration"))
            result.Methods.Add(ParseMethod(method, parsedClass, fields, superName));
    }

    private static ParsedMethod ParseMethod(Node method, ParsedClass owner, Dictionary<string, string> fields, string? superName)
    {
        var name = method.FieldText("name") ?? "unknown";
        var parameters = new List<ParsedParameter>();
        var locals = new Dictionary<string, string>(StringComparer.Ordinal);

        if (method.Field("parameters") is { } paramList)
        {
            foreach (var p in paramList.NamedChildren.Where(c => c.Type is "formal_parameter" or "spread_parameter"))
            {
                var pName = p.FieldText("name") ?? p.NamedChildren.LastOrDefault(c => c.Type is "identifier" or "variable_declarator")?.Text ?? "arg";
                var pType = p.FieldText("type") ?? p.NamedChildren.FirstOrDefault(c => c.Type.EndsWith("type") || c.Type == "type_identifier")?.Text;
                if (p.Type == "spread_parameter" && pType is not null) pType += "...";
                parameters.Add(new ParsedParameter(pName, pType));
                locals[pName] = ParserText.SimpleTypeName(pType);
            }
        }

        var modifiers = method.NamedChildren.FirstOrDefault(c => c.Type == "modifiers");
        var (verb, route) = modifiers is null ? (null, null) : FindMapping(modifiers);
        var body = method.Field("body");

        var parsed = new ParsedMethod
        {
            ClassName = owner.Name,
            MethodName = name,
            FilePath = owner.FilePath,
            Language = ProgrammingLanguage.Java,
            SourceCode = ParserText.Source(method.Text),
            StartLine = method.Line(),
            Parameters = parameters,
            ReturnType = method.FieldText("type"),
            IsAbstract = body is null,
            IsDecision = body is not null && body.Descendants().Any(n => BranchNodes.Contains(n.Type)),
            HttpVerb = verb,
            Route = verb is null ? null : ParserText.CombineRoute(owner.RoutePrefix, route)
        };

        if (body is null) return parsed;

        foreach (var node in body.Descendants())
        {
            if (node.Type == "local_variable_declaration")
            {
                var declaredType = ParserText.SimpleTypeName(node.FieldText("type"));
                foreach (var declarator in node.NamedChildren.Where(c => c.Type == "variable_declarator"))
                {
                    var varName = declarator.FieldText("name");
                    if (string.IsNullOrEmpty(varName)) continue;
                    var value = declarator.Field("value");
                    locals[varName] = declaredType == "var" && value?.Type == "object_creation_expression"
                        ? ParserText.SimpleTypeName(value.FieldText("type"))
                        : declaredType;
                }
                continue;
            }

            if (node.Type == "enhanced_for_statement")
            {
                var loopVar = node.FieldText("name");
                if (!string.IsNullOrEmpty(loopVar)) locals[loopVar] = ParserText.SimpleTypeName(node.FieldText("type"));
                continue;
            }

            if (node.Type != "method_invocation") continue;

            var calledName = node.FieldText("name");
            if (string.IsNullOrEmpty(calledName)) continue;

            var receiverType = ResolveReceiver(node.Field("object"), owner.Name, fields, locals, superName);
            if (receiverType is null) continue;

            parsed.Calls.Add(new ParsedCall
            {
                ReceiverType = receiverType,
                MethodName = calledName,
                Condition = FindCondition(node, body),
                ArgumentCount = node.Field("arguments")?.NamedChildren.Count ?? -1
            });
        }

        return parsed;
    }

    private static string? ResolveReceiver(
        Node? receiver,
        string className,
        Dictionary<string, string> fields,
        Dictionary<string, string> locals,
        string? superName)
    {
        if (receiver is null || receiver.Type == "this") return className;
        if (receiver.Type == "super") return superName;

        if (receiver.Type == "identifier")
        {
            var name = receiver.Text;
            if (locals.TryGetValue(name, out var localType)) return NullIfEmpty(localType);
            if (fields.TryGetValue(name, out var fieldType)) return NullIfEmpty(fieldType);
            return char.IsUpper(name[0]) ? name : null;
        }

        if (receiver.Type == "field_access" && receiver.Field("object")?.Type == "this")
        {
            var fieldName = receiver.FieldText("field");
            return fieldName is not null && fields.TryGetValue(fieldName, out var t) ? NullIfEmpty(t) : null;
        }

        return null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) || value == "var" ? null : value;

    private static string? FindCondition(Node node, Node stopAt)
    {
        var child = node;
        for (var current = node.Parent; current is not null && current != stopAt; child = current, current = current.Parent)
        {
            switch (current.Type)
            {
                case "if_statement":
                {
                    var condition = current.Field("condition");
                    if (condition is not null && child.IsWithin(condition)) break;
                    var text = ParserText.StripParens(condition?.Text ?? "?");
                    var alternative = current.Field("alternative");
                    return ParserText.OneLine(alternative is not null && child.IsWithin(alternative) ? $"!({text})" : text);
                }
                case "ternary_expression":
                {
                    var condition = current.Field("condition");
                    if (condition is not null && child.IsWithin(condition)) break;
                    var text = ParserText.StripParens(condition?.Text ?? "?");
                    var alternative = current.Field("alternative");
                    return ParserText.OneLine(alternative is not null && child.IsWithin(alternative) ? $"!({text})" : text);
                }
                case "switch_block_statement_group":
                case "switch_rule":
                {
                    var label = current.NamedChildren.FirstOrDefault(c => c.Type == "switch_label");
                    return ParserText.OneLine(label?.Text ?? "case");
                }
                case "catch_clause":
                    return ParserText.OneLine($"catch {current.NamedChildren.FirstOrDefault(c => c.Type == "catch_formal_parameter")?.Text}");
                case "enhanced_for_statement":
                case "for_statement":
                case "while_statement":
                {
                    var loopBody = current.Field("body");
                    if (loopBody is null || !child.IsWithin(loopBody)) break;
                    var header = current.Text[..Math.Max(0, loopBody.StartIndex - current.StartIndex)];
                    return ParserText.OneLine(header);
                }
            }
        }
        return null;
    }

    private static (string? Verb, string? Route) FindMapping(Node modifiers)
    {
        foreach (var annotation in modifiers.NamedChildren.Where(c => c.Type is "annotation" or "marker_annotation"))
        {
            var annotationName = ParserText.SimpleTypeName(annotation.FieldText("name"));
            if (!MappingAnnotations.TryGetValue(annotationName, out var verb)) continue;

            string? route = null;
            var args = annotation.Field("arguments");
            if (args is not null)
            {
                foreach (var arg in args.NamedChildren)
                {
                    if (arg.Type == "string_literal")
                    {
                        route ??= ParserText.StringLiteralValue(arg.Text);
                    }
                    else if (arg.Type == "element_value_pair")
                    {
                        var key = arg.FieldText("key");
                        var value = arg.Field("value");
                        if (key is "value" or "path")
                        {
                            var literal = value?.Type == "string_literal" ? value : value?.Descendants().FirstOrDefault(d => d.Type == "string_literal");
                            route ??= ParserText.StringLiteralValue(literal?.Text);
                        }
                        else if (key == "method" && value is not null)
                        {
                            var methodText = value.Text;
                            foreach (var candidate in new[] { "GET", "POST", "PUT", "DELETE", "PATCH" })
                                if (methodText.Contains(candidate, StringComparison.Ordinal)) { verb = candidate; break; }
                        }
                    }
                    else if (arg.Type == "element_value_array_initializer")
                    {
                        route ??= ParserText.StringLiteralValue(arg.NamedChildren.FirstOrDefault(c => c.Type == "string_literal")?.Text);
                    }
                }
            }

            return (verb, route);
        }
        return (null, null);
    }

    private static IEnumerable<string> TypeNames(Node node)
        => node.NamedChildren
            .SelectMany(c => c.Type == "type_list" ? c.NamedChildren : (IEnumerable<Node>)[c])
            .Select(t => ParserText.SimpleTypeName(t.Text))
            .Where(t => !string.IsNullOrEmpty(t));
}
