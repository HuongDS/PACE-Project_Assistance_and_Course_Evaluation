using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Common;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Analysis.Parsing;

public sealed class CSharpRoslynParser : ILanguageParser
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(LoadReferences);

    private static readonly Dictionary<string, string> HttpAttributes = new(StringComparer.Ordinal)
    {
        ["HttpGet"] = "GET",
        ["HttpPost"] = "POST",
        ["HttpPut"] = "PUT",
        ["HttpDelete"] = "DELETE",
        ["HttpPatch"] = "PATCH"
    };

    public string Name => "C# (Roslyn)";

    public SourceKind Kind => SourceKind.CSharp;

    public ParseResult Parse(IReadOnlyList<SourceFile> files, CancellationToken ct)
    {
        var result = new ParseResult();
        if (files.Count == 0) return result;

        var trees = files
            .Select(f => CSharpSyntaxTree.ParseText(f.Content, path: f.RelativePath, cancellationToken: ct))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "PaceRepositoryAnalysis",
            trees,
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var mediatorHandlers = BuildMediatorHandlerMap(trees, compilation);

        foreach (var tree in trees)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var model = compilation.GetSemanticModel(tree);
                var root = tree.GetCompilationUnitRoot(ct);
                foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                    ParseType(type, model, tree.FilePath, mediatorHandlers, result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.Warnings.Add($"Lỗi parse C# {tree.FilePath}: {ex.Message}");
            }
        }

        return result;
    }

    private static void ParseType(
        TypeDeclarationSyntax type,
        SemanticModel model,
        string filePath,
        IReadOnlyDictionary<string, List<string>> mediatorHandlers,
        ParseResult result)
    {
        var className = NestedName(type);
        if (IsMigrationType(type)) return;

        var parsedClass = new ParsedClass
        {
            Name = className,
            Kind = type switch
            {
                InterfaceDeclarationSyntax => "interface",
                RecordDeclarationSyntax => "record",
                StructDeclarationSyntax => "struct",
                _ => "class"
            },
            FilePath = filePath,
            Language = ProgrammingLanguage.CSharp,
            RoutePrefix = FindAttributeArgument(type.AttributeLists, "Route")
        };
        if (type.BaseList is not null)
            parsedClass.BaseTypes.AddRange(type.BaseList.Types.Select(t => ParserText.SimpleTypeName(t.Type.ToString())));
        result.Classes.Add(parsedClass);

        foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
        {
            var (verb, route) = FindHttpMapping(method.AttributeLists);
            var parsed = new ParsedMethod
            {
                ClassName = className,
                MethodName = method.Identifier.Text,
                FilePath = filePath,
                Language = ProgrammingLanguage.CSharp,
                SourceCode = ParserText.Source(method.ToString()),
                StartLine = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Parameters = method.ParameterList.Parameters
                    .Select(p => new ParsedParameter(p.Identifier.Text, p.Type?.ToString()))
                    .ToList(),
                ReturnType = method.ReturnType.ToString(),
                IsAbstract = method.Body is null && method.ExpressionBody is null,
                IsDecision = method.DescendantNodes().Any(IsBranchNode),
                HttpVerb = verb,
                Route = verb is null && route is null ? null : ParserText.CombineRoute(parsedClass.RoutePrefix, route)
            };

            SyntaxNode? body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
            if (body is not null)
            {
                foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
                    CollectCalls(invocation, method, className, model, mediatorHandlers, parsed.Calls);
            }

            result.Methods.Add(parsed);
        }
    }

    private static void CollectCalls(
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax owner,
        string className,
        SemanticModel model,
        IReadOnlyDictionary<string, List<string>> mediatorHandlers,
        List<ParsedCall> calls)
    {
        var condition = FindCondition(invocation, owner);
        var argCount = invocation.ArgumentList.Arguments.Count;
        var symbolInfo = model.GetSymbolInfo(invocation);
        var symbol = symbolInfo.Symbol as IMethodSymbol ?? symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

        if (symbol is not null)
        {
            var declaring = symbol.ReducedFrom?.ContainingType ?? symbol.ContainingType;
            if (declaring is not null && declaring.Locations.Any(l => l.IsInSource))
            {
                calls.Add(new ParsedCall
                {
                    ReceiverType = NestedName(declaring),
                    MethodName = symbol.Name,
                    Condition = condition,
                    ArgumentCount = symbol.ReducedFrom is null ? argCount : argCount + 1
                });
            }
            return;
        }

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var name = memberAccess.Name.Identifier.Text;
            if (name is "Send" or "Publish" && TryMediatorTarget(invocation, model, mediatorHandlers, out var handlers))
            {
                foreach (var handler in handlers)
                    calls.Add(new ParsedCall { ReceiverType = handler, MethodName = "Handle", Condition = condition });
                return;
            }

            var receiverType = model.GetTypeInfo(memberAccess.Expression).Type;
            if (receiverType is not null && receiverType.TypeKind != TypeKind.Error && receiverType.Locations.Any(l => l.IsInSource))
                calls.Add(new ParsedCall { ReceiverType = receiverType is INamedTypeSymbol named ? NestedName(named) : receiverType.Name, MethodName = name, Condition = condition, ArgumentCount = argCount });
            return;
        }

        if (invocation.Expression is IdentifierNameSyntax identifier)
            calls.Add(new ParsedCall { ReceiverType = className, MethodName = identifier.Identifier.Text, Condition = condition, ArgumentCount = argCount });
    }

    private static bool TryMediatorTarget(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IReadOnlyDictionary<string, List<string>> mediatorHandlers,
        out List<string> handlers)
    {
        handlers = [];
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (argument is null || mediatorHandlers.Count == 0) return false;

        var requestType = model.GetTypeInfo(argument).Type is INamedTypeSymbol { TypeKind: not TypeKind.Error } named ? NestedName(named) : null;
        if (string.IsNullOrEmpty(requestType) && argument is ObjectCreationExpressionSyntax creation)
            requestType = model.GetSymbolInfo(creation.Type).Symbol is INamedTypeSymbol created ? NestedName(created) : creation.Type.ToString();

        if (string.IsNullOrEmpty(requestType) || !mediatorHandlers.TryGetValue(requestType, out var found)) return false;
        handlers = found;
        return true;
    }

    private static Dictionary<string, List<string>> BuildMediatorHandlerMap(IEnumerable<SyntaxTree> trees, Compilation compilation)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var tree in trees)
        {
            SemanticModel? model = null;
            foreach (var type in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (type.BaseList is null) continue;
                foreach (var baseType in type.BaseList.Types)
                {
                    if (baseType.Type is not GenericNameSyntax generic) continue;
                    if (generic.Identifier.Text is not ("IRequestHandler" or "INotificationHandler")) continue;
                    if (generic.TypeArgumentList.Arguments.Count == 0) continue;

                    model ??= compilation.GetSemanticModel(tree);
                    var argument = generic.TypeArgumentList.Arguments[0];
                    var request = model.GetSymbolInfo(argument).Symbol is INamedTypeSymbol symbol
                        ? NestedName(symbol)
                        : argument.ToString();

                    var handler = NestedName(type);
                    if (!map.TryGetValue(request, out var list)) map[request] = list = [];
                    if (!list.Contains(handler)) list.Add(handler);
                }
            }
        }
        return map;
    }

    private static string NestedName(TypeDeclarationSyntax type)
        => string.Join(".", type.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Select(t => t.Identifier.Text).Reverse());

    private static string NestedName(INamedTypeSymbol type)
    {
        var parts = new List<string>();
        for (var current = type; current is not null; current = current.ContainingType) parts.Add(current.Name);
        parts.Reverse();
        return string.Join(".", parts);
    }

    private static string? FindCondition(SyntaxNode node, SyntaxNode stopAt)
    {
        var child = node;
        for (var current = node.Parent; current is not null && current != stopAt; child = current, current = current.Parent)
        {
            switch (current)
            {
                case IfStatementSyntax ifStatement when child == ifStatement.Statement:
                    return ParserText.OneLine(ifStatement.Condition.ToString());
                case ElseClauseSyntax elseClause when elseClause.Parent is IfStatementSyntax parentIf:
                    return ParserText.OneLine($"!({parentIf.Condition})");
                case ConditionalExpressionSyntax ternary when child == ternary.WhenTrue:
                    return ParserText.OneLine(ternary.Condition.ToString());
                case ConditionalExpressionSyntax ternary when child == ternary.WhenFalse:
                    return ParserText.OneLine($"!({ternary.Condition})");
                case SwitchSectionSyntax section:
                    return ParserText.OneLine(string.Join(" / ", section.Labels.Select(l => l.ToString().TrimEnd(':'))));
                case SwitchExpressionArmSyntax arm:
                    return ParserText.OneLine($"case {arm.Pattern}");
                case CatchClauseSyntax catchClause:
                    return ParserText.OneLine($"catch {catchClause.Declaration?.Type}");
                case ForEachStatementSyntax forEach when child == forEach.Statement:
                    return ParserText.OneLine($"foreach {forEach.Identifier.Text} in {forEach.Expression}");
                case ForStatementSyntax forStatement when child == forStatement.Statement:
                    return ParserText.OneLine($"for {forStatement.Condition}");
                case WhileStatementSyntax whileStatement when child == whileStatement.Statement:
                    return ParserText.OneLine($"while {whileStatement.Condition}");
            }
        }
        return null;
    }

    private static bool IsBranchNode(SyntaxNode n) => n is IfStatementSyntax or SwitchStatementSyntax or SwitchExpressionSyntax
        or ConditionalExpressionSyntax or WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax or ForEachStatementSyntax;

    private static (string? Verb, string? Route) FindHttpMapping(SyntaxList<AttributeListSyntax> attributeLists)
    {
        string? verb = null, route = null;
        foreach (var attribute in attributeLists.SelectMany(l => l.Attributes))
        {
            var name = ParserText.SimpleTypeName(attribute.Name.ToString());
            if (name.EndsWith("Attribute", StringComparison.Ordinal)) name = name[..^"Attribute".Length];

            if (HttpAttributes.TryGetValue(name, out var mapped))
            {
                verb = mapped;
                route ??= FirstStringArgument(attribute);
            }
            else if (name == "Route")
            {
                route = FirstStringArgument(attribute) ?? route;
            }
        }
        return (verb, route);
    }

    private static string? FindAttributeArgument(SyntaxList<AttributeListSyntax> attributeLists, string attributeName)
        => attributeLists.SelectMany(l => l.Attributes)
            .Where(a => ParserText.SimpleTypeName(a.Name.ToString()) is var n && (n == attributeName || n == attributeName + "Attribute"))
            .Select(FirstStringArgument)
            .FirstOrDefault(v => v is not null);

    private static string? FirstStringArgument(AttributeSyntax attribute)
        => attribute.ArgumentList?.Arguments
            .Select(a => a.Expression)
            .OfType<LiteralExpressionSyntax>()
            .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression))
            .Select(l => l.Token.ValueText)
            .FirstOrDefault();

    private static bool IsMigrationType(TypeDeclarationSyntax type)
        => type.BaseList?.Types.Any(t => ParserText.SimpleTypeName(t.Type.ToString()) is "Migration" or "ModelSnapshot") == true;

    private static IReadOnlyList<MetadataReference> LoadReferences()
    {
        var references = new List<MetadataReference>();
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trusted))
        {
            foreach (var path in trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(path));
                }
                catch (Exception ex) when (ex is IOException or BadImageFormatException)
                {
                }
            }
        }

        if (references.Count == 0) references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        return references;
    }
}
