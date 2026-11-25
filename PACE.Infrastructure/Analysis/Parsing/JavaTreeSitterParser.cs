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

    public string Name => "Java (tree-sitter)";

    public SourceKind Kind => SourceKind.Java;

    public ParseResult Parse(IReadOnlyList<SourceFile> files, CancellationToken ct)
    {
        var result = new ParseResult();
        if (files.Count == 0) return result;

        using var language = new Language("Java");
        using var parser = new Parser(language);

        var sources = new List<JavaSourcePositions>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var tree = parser.Parse(file.Content);
                if (tree is null)
                {
                    result.HasSyntaxReadFailure = true;
                    result.Warnings.Add($"Không parse được Java {file.RelativePath}");
                    continue;
                }

                var positions = new JavaSourcePositions(file);
                var mappings = new JavaSpringMappings(tree.RootNode);
                foreach (var type in tree.RootNode.Descendants().Where(n => TypeDeclarations.Contains(n.Type)))
                    ParseType(type, positions, mappings, result);
                sources.Add(positions);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.HasSyntaxReadFailure = true;
                result.Warnings.Add($"Lỗi parse Java {file.RelativePath}: {ex.Message}");
            }
        }

        if (sources.Any(source => source.Calls.Count > 0 || source.SpringMethods.Values.Any(mapping => mapping.IsController)))
            JavaCompilerBindings.Apply(sources, result, ct);
        JavaSpringMappings.Apply(sources, result);
        return result;
    }

    private static void ParseType(Node type, JavaSourcePositions positions, JavaSpringMappings mappings, ParseResult result)
    {
        var className = type.FieldText("name");
        if (string.IsNullOrEmpty(className)) return;

        var modifiers = type.NamedChildren.FirstOrDefault(c => c.Type == "modifiers");
        var typeMapping = mappings.Read(modifiers);
        var isController = type.Type != "interface_declaration" && mappings.IsController(modifiers) &&
            modifiers?.Children.Any(node => node.Type == "abstract") != true;
        var parsedClass = new ParsedClass
        {
            Name = className,
            Kind = type.Type.Replace("_declaration", string.Empty),
            FilePath = positions.Source.RelativePath,
            Language = ProgrammingLanguage.Java,
            RoutePrefix = typeMapping?.Paths.FirstOrDefault()
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

        foreach (var method in members.Where(m => m.Type == "method_declaration"))
        {
            var parsed = ParseMethod(method, parsedClass, positions);
            result.Methods.Add(parsed);
            positions.Methods[method.StartIndex] = parsed;
            positions.SpringMethods[parsed] = new SpringMethodMapping(isController, typeMapping,
                mappings.Read(method.NamedChildren.FirstOrDefault(node => node.Type == "modifiers")));
        }
    }

    private static ParsedMethod ParseMethod(Node method, ParsedClass owner, JavaSourcePositions positions)
    {
        var name = method.FieldText("name") ?? "unknown";
        var parameters = new List<ParsedParameter>();

        if (method.Field("parameters") is { } paramList)
        {
            foreach (var p in paramList.NamedChildren.Where(c => c.Type is "formal_parameter" or "spread_parameter"))
            {
                var pName = p.FieldText("name") ?? p.NamedChildren.LastOrDefault(c => c.Type is "identifier" or "variable_declarator")?.Text ?? "arg";
                var pType = p.FieldText("type") ?? p.NamedChildren.FirstOrDefault(c => c.Type.EndsWith("type") || c.Type == "type_identifier")?.Text;
                if (p.Type == "spread_parameter" && pType is not null) pType += "...";
                parameters.Add(new ParsedParameter(pName, pType));
            }
        }

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
            IsDecision = body is not null && body.Descendants().Any(n => BranchNodes.Contains(n.Type))
        };

        if (body is null) return parsed;

        foreach (var node in body.Descendants(child =>
                     !TypeDeclarations.Contains(child.Type) && child.Type != "class_body"))
        {
            if (node.Type != "method_invocation" || node.FieldText("name") is not { Length: > 0 } calledName) continue;
            var call = new ParsedCall
            {
                MethodName = calledName,
                Condition = FindCondition(node, body)
            };
            parsed.Calls.Add(call);
            positions.Calls[(node.StartIndex, node.EndIndex)] = call;
        }

        return parsed;
    }

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

    private static IEnumerable<string> TypeNames(Node node)
        => node.NamedChildren
            .SelectMany(c => c.Type == "type_list" ? c.NamedChildren : (IEnumerable<Node>)[c])
            .Select(t => ParserText.SimpleTypeName(t.Text))
            .Where(t => !string.IsNullOrEmpty(t));
}
