using TreeSitter;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Common;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Analysis.Parsing;

public sealed class JsTsTreeSitterParser : ILanguageParser
{
    public string Name => "JavaScript/TypeScript (tree-sitter)";

    public SourceKind Kind => SourceKind.JavaScriptTypeScript;

    public ParseResult Parse(IReadOnlyList<SourceFile> files, CancellationToken ct)
    {
        var result = new ParseResult();
        if (files.Count == 0) return result;

        using var jsLanguage = new Language("JavaScript");
        using var tsLanguage = new Language("TypeScript");
        using var tsxLanguage = new Language("TSX");
        using var jsParser = new Parser(jsLanguage);
        using var tsParser = new Parser(tsLanguage);
        using var tsxParser = new Parser(tsxLanguage);

        var routes = new List<JsRouteBinding>();
        var reexports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file.RelativePath).ToLowerInvariant();
            var (parser, language) = ext switch
            {
                ".ts" => (tsParser, ProgrammingLanguage.TypeScript),
                ".tsx" => (tsxParser, ProgrammingLanguage.TypeScript),
                _ => (jsParser, ProgrammingLanguage.JavaScript)
            };

            try
            {
                using var tree = parser.Parse(file.Content);
                if (tree is null)
                {
                    result.Warnings.Add($"Không parse được {file.RelativePath}");
                    continue;
                }

                new JsFileAnalyzer(file.RelativePath, language, result, routes, reexports).Analyze(tree.RootNode);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result.Warnings.Add($"Lỗi parse {file.RelativePath}: {ex.Message}");
            }
        }

        foreach (var call in result.Methods.SelectMany(m => m.Calls))
            call.ReceiverType = JsFileAnalyzer.ResolvePlaceholder(call.ReceiverType, reexports);

        foreach (var route in routes)
        {
            var targetClass = JsFileAnalyzer.ResolvePlaceholder(route.TargetClass, reexports);
            var target = result.Methods.FirstOrDefault(m =>
                string.Equals(m.ClassName, targetClass, StringComparison.OrdinalIgnoreCase)
                && string.Equals(m.MethodName, route.TargetMethod, StringComparison.Ordinal));
            if (target is null || target.HttpVerb is not null) continue;
            target.HttpVerb = route.Verb;
            target.Route = route.Path;
        }

        return result;
    }

    internal sealed record JsRouteBinding(string Verb, string Path, string TargetClass, string TargetMethod);
}

internal sealed class JsFileAnalyzer
{
    private static readonly HashSet<string> FunctionNodes = ["arrow_function", "function_expression", "function", "generator_function"];
    private static readonly HashSet<string> RouteVerbs = ["get", "post", "put", "patch", "delete", "all"];
    private static readonly HashSet<string> NestVerbs = ["Get", "Post", "Put", "Patch", "Delete", "All"];

    private static readonly HashSet<string> BranchNodes =
    [
        "if_statement", "switch_statement", "ternary_expression", "for_statement", "for_in_statement",
        "while_statement", "do_statement"
    ];

    private readonly string _filePath;
    private readonly ProgrammingLanguage _language;
    private readonly ParseResult _result;
    private readonly List<JsTsTreeSitterParser.JsRouteBinding> _routes;
    private readonly string _module;
    private readonly Dictionary<string, string> _reexports;

    private readonly Dictionary<string, (string Module, string? Export)> _imports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _moduleInstances = new(StringComparer.Ordinal);
    private readonly HashSet<string> _moduleFunctionNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _classNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _emittedMethods = new(StringComparer.Ordinal);

    private sealed record FunctionCandidate(string Name, Node Function, Node Source);

    private sealed class ClassContext
    {
        public required string Name { get; init; }
        public Dictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);
        public string? BaseClass { get; set; }
    }

    public JsFileAnalyzer(string filePath, ProgrammingLanguage language, ParseResult result, List<JsTsTreeSitterParser.JsRouteBinding> routes, Dictionary<string, string> reexports)
    {
        _reexports = reexports;
        _filePath = filePath;
        _language = language;
        _result = result;
        _routes = routes;
        _module = ModuleName(filePath);
    }

    public static string? ResolvePlaceholder(string? receiver, IReadOnlyDictionary<string, string> reexports)
    {
        for (var hop = 0; receiver is not null && hop < 5; hop++)
        {
            var separator = receiver.IndexOf("::", StringComparison.Ordinal);
            if (separator < 0) return receiver;
            receiver = reexports.TryGetValue(receiver, out var target) ? target : receiver[..separator];
        }
        return receiver;
    }

    private static string ImportReceiver((string Module, string? Export) imported)
    {
        if (imported.Export is null) return imported.Module;
        if (imported.Export.Length > 0 && char.IsUpper(imported.Export[0])) return imported.Export;
        return $"{imported.Module}::{imported.Export}";
    }

    private void AddReexport(string exportName, Node value)
    {
        string? target = null;
        if (TryRequirePath(value, out var path)) target = ImportModuleName(path);
        else if (value.Type is "identifier" or "shorthand_property_identifier" && _imports.TryGetValue(value.Text, out var imported)) target = ImportReceiver(imported);
        if (target is not null) _reexports[$"{_module}::{exportName}"] = target;
    }

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase) { ".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx" };

    private static string ImportModuleName(string importPath)
    {
        var trimmed = importPath.TrimEnd('/');
        if (!CodeExtensions.Contains(Path.GetExtension(trimmed))) trimmed += ".js";
        return ModuleName(trimmed);
    }

    public static string ModuleName(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        var dot = fileName.LastIndexOf('.');
        var stem = dot > 0 ? fileName[..dot] : fileName;
        if (stem.Equals("index", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Path.GetFileName(Path.GetDirectoryName(relativePath) ?? string.Empty);
            if (!string.IsNullOrEmpty(parent)) stem = parent;
        }
        return ParserText.PascalCase(stem);
    }

    public void Analyze(Node root)
    {
        var functions = new List<FunctionCandidate>();
        var classes = new List<(Node Class, Node Source, List<Node> Decorators)>();

        foreach (var statement in root.NamedChildren)
            CollectTopLevel(statement, statement, [], functions, classes);

        foreach (var c in classes)
        {
            var name = c.Class.FieldText("name");
            if (!string.IsNullOrEmpty(name)) _classNames.Add(name);
        }

        if (functions.Count > 0 && !_classNames.Contains(_module))
        {
            _result.Classes.Add(new ParsedClass { Name = _module, Kind = "module", FilePath = _filePath, Language = _language });
        }

        foreach (var fn in functions)
            EmitMethod(fn.Function, fn.Source, _module, fn.Name, null, null, null);

        foreach (var (classNode, source, decorators) in classes)
            AnalyzeClass(classNode, source, decorators);

        CollectRoutes(root);
    }

    private void CollectTopLevel(Node statement, Node source, List<Node> decorators, List<FunctionCandidate> functions, List<(Node, Node, List<Node>)> classes)
    {
        switch (statement.Type)
        {
            case "export_statement":
            {
                var exportDecorators = statement.NamedChildren.Where(c => c.Type == "decorator").ToList();
                var declaration = statement.Field("declaration");
                if (declaration is not null)
                {
                    CollectTopLevel(declaration, statement, exportDecorators, functions, classes);
                    return;
                }

                var value = statement.Field("value") ?? statement.NamedChildren.LastOrDefault(c => c.Type != "decorator");
                if (value?.Type == "object") CollectObjectExports(value, functions);
                else if (value is not null && UnwrapFunction(value) is { } fn) AddFunction(functions, "default", fn, statement);
                return;
            }
            case "import_statement":
                CollectImport(statement);
                return;
            case "function_declaration":
            case "generator_function_declaration":
            {
                var name = statement.FieldText("name");
                if (!string.IsNullOrEmpty(name)) AddFunction(functions, name, statement, source);
                return;
            }
            case "class_declaration":
            case "abstract_class_declaration":
            case "class":
                classes.Add((statement, source, decorators.Concat(statement.NamedChildren.Where(c => c.Type == "decorator")).ToList()));
                return;
            case "lexical_declaration":
            case "variable_declaration":
                foreach (var declarator in statement.NamedChildren.Where(c => c.Type == "variable_declarator"))
                    CollectDeclarator(declarator, source, functions, classes);
                return;
            case "expression_statement":
            {
                var expression = statement.NamedChildren.FirstOrDefault();
                if (expression?.Type == "assignment_expression") CollectAssignment(expression, source, functions);
                return;
            }
        }
    }

    private void CollectDeclarator(Node declarator, Node source, List<FunctionCandidate> functions, List<(Node, Node, List<Node>)> classes)
    {
        var nameNode = declarator.Field("name");
        var value = declarator.Field("value");
        if (nameNode is null || value is null) return;

        if (TryRequirePath(value, out var modulePath))
        {
            RegisterImport(nameNode, modulePath);
            return;
        }

        if (nameNode.Type != "identifier") return;
        var name = nameNode.Text;

        if (value.Type == "new_expression")
        {
            var ctor = value.FieldText("constructor");
            if (!string.IsNullOrEmpty(ctor)) _moduleInstances[name] = ParserText.SimpleTypeName(ctor);
            return;
        }

        if (value.Type == "class")
        {
            classes.Add((value, source, []));
            return;
        }

        if (UnwrapFunction(value) is { } fn) AddFunction(functions, name, fn, source);
    }

    private void CollectAssignment(Node assignment, Node source, List<FunctionCandidate> functions)
    {
        var left = assignment.Field("left");
        var right = assignment.Field("right");
        if (left is null || right is null) return;

        var leftText = left.Text.Replace(" ", string.Empty);
        if (leftText == "module.exports")
        {
            if (right.Type == "object") CollectObjectExports(right, functions);
            else if (UnwrapFunction(right) is { } fn) AddFunction(functions, "default", fn, source);
            return;
        }

        string? exportName = null;
        if (leftText.StartsWith("exports.", StringComparison.Ordinal)) exportName = leftText["exports.".Length..];
        else if (leftText.StartsWith("module.exports.", StringComparison.Ordinal)) exportName = leftText["module.exports.".Length..];
        if (string.IsNullOrEmpty(exportName) || exportName.Contains('.')) return;

        if (UnwrapFunction(right) is { } exported) AddFunction(functions, exportName, exported, source);
        else AddReexport(exportName, right);
    }

    private void CollectObjectExports(Node obj, List<FunctionCandidate> functions)
    {
        foreach (var member in obj.NamedChildren)
        {
            if (member.Type == "method_definition")
            {
                var name = member.FieldText("name");
                if (!string.IsNullOrEmpty(name)) AddFunction(functions, name, member, member);
            }
            else if (member.Type == "pair")
            {
                var key = ParserText.StringLiteralValue(member.FieldText("key")) ?? member.FieldText("key");
                var value = member.Field("value");
                if (string.IsNullOrEmpty(key) || value is null) continue;
                if (UnwrapFunction(value) is { } fn) AddFunction(functions, key, fn, member);
                else AddReexport(key, value);
            }
            else if (member.Type == "shorthand_property_identifier")
            {
                AddReexport(member.Text, member);
            }
        }
    }

    private void AddFunction(List<FunctionCandidate> functions, string name, Node function, Node source)
    {
        if (!_moduleFunctionNames.Add(name)) return;
        functions.Add(new FunctionCandidate(name, function, source));
    }

    private static Node? UnwrapFunction(Node value)
    {
        if (FunctionNodes.Contains(value.Type)) return value;
        if (value.Type == "parenthesized_expression") return value.NamedChildren.Select(UnwrapFunction).FirstOrDefault(f => f is not null);
        if (value.Type == "call_expression")
        {
            var args = value.Field("arguments");
            return args?.NamedChildren.LastOrDefault(a => FunctionNodes.Contains(a.Type));
        }
        return null;
    }

    private void CollectImport(Node statement)
    {
        var sourceNode = statement.Field("source");
        var path = ParserText.StringLiteralValue(sourceNode?.Text);
        if (path is null || !IsRelative(path)) return;
        var module = ImportModuleName(path);

        var clause = statement.NamedChildren.FirstOrDefault(c => c.Type == "import_clause");
        if (clause is null) return;

        foreach (var part in clause.NamedChildren)
        {
            switch (part.Type)
            {
                case "identifier":
                    _imports[part.Text] = (module, null);
                    break;
                case "namespace_import":
                    var ns = part.NamedChildren.FirstOrDefault(c => c.Type == "identifier");
                    if (ns is not null) _imports[ns.Text] = (module, null);
                    break;
                case "named_imports":
                    foreach (var spec in part.NamedChildren.Where(c => c.Type == "import_specifier"))
                    {
                        var original = spec.FieldText("name");
                        var alias = spec.FieldText("alias") ?? original;
                        if (!string.IsNullOrEmpty(original) && !string.IsNullOrEmpty(alias)) _imports[alias] = (module, original);
                    }
                    break;
            }
        }
    }

    private void RegisterImport(Node nameNode, string modulePath)
    {
        var module = ImportModuleName(modulePath);
        if (nameNode.Type == "identifier")
        {
            _imports[nameNode.Text] = (module, null);
            return;
        }

        if (nameNode.Type != "object_pattern") return;
        foreach (var part in nameNode.NamedChildren)
        {
            if (part.Type == "shorthand_property_identifier_pattern")
            {
                _imports[part.Text] = (module, part.Text);
            }
            else if (part.Type == "pair_pattern")
            {
                var key = part.FieldText("key");
                var value = part.Field("value");
                if (!string.IsNullOrEmpty(key) && value?.Type == "identifier") _imports[value.Text] = (module, key);
            }
        }
    }

    private static bool TryRequirePath(Node value, out string path)
    {
        path = string.Empty;
        var call = value.Type == "await_expression" ? value.NamedChildren.FirstOrDefault() : value;
        if (call?.Type == "member_expression") call = call.Field("object");
        if (call?.Type != "call_expression" || call.FieldText("function") != "require") return false;

        var literal = ParserText.StringLiteralValue(call.Field("arguments")?.NamedChildren.FirstOrDefault()?.Text);
        if (literal is null || !IsRelative(literal)) return false;
        path = literal;
        return true;
    }

    private static bool IsRelative(string path) => path.StartsWith('.') || path.StartsWith('/') || path.StartsWith("@/") || path.StartsWith("~/");

    private void AnalyzeClass(Node classNode, Node source, List<Node> decorators)
    {
        var name = classNode.FieldText("name");
        if (string.IsNullOrEmpty(name)) return;

        var parsedClass = new ParsedClass
        {
            Name = name,
            Kind = "class",
            FilePath = _filePath,
            Language = _language,
            RoutePrefix = decorators.Select(d => DecoratorCall(d)).FirstOrDefault(d => d.Name == "Controller").Argument
        };

        var context = new ClassContext { Name = name };
        var heritage = classNode.NamedChildren.FirstOrDefault(c => c.Type == "class_heritage");
        if (heritage is not null)
        {
            foreach (var clause in heritage.NamedChildren)
            {
                if (clause.Type == "extends_clause")
                {
                    var baseName = ParserText.SimpleTypeName(clause.FieldText("value") ?? clause.NamedChildren.FirstOrDefault()?.Text);
                    if (!string.IsNullOrEmpty(baseName))
                    {
                        parsedClass.BaseTypes.Add(baseName);
                        context.BaseClass = baseName;
                    }
                }
                else if (clause.Type == "implements_clause")
                {
                    parsedClass.BaseTypes.AddRange(clause.NamedChildren.Select(t => ParserText.SimpleTypeName(t.Text)).Where(t => t.Length > 0));
                }
            }

            if (heritage.NamedChildren.All(c => c.Type is not ("extends_clause" or "implements_clause")) && heritage.NamedChildren.FirstOrDefault() is { } bare)
            {
                var baseName = ParserText.SimpleTypeName(bare.Text);
                parsedClass.BaseTypes.Add(baseName);
                context.BaseClass = baseName;
            }
        }
        _result.Classes.Add(parsedClass);

        var body = classNode.Field("body");
        if (body is null) return;

        foreach (var member in body.NamedChildren)
        {
            if (member.Type is "public_field_definition" or "field_definition")
            {
                var fieldName = (member.Field("name") ?? member.Field("property"))?.Text;
                if (string.IsNullOrEmpty(fieldName)) continue;
                var type = TypeAnnotation(member.Field("type"));
                var value = member.Field("value");
                if (type is null && value?.Type == "new_expression") type = ParserText.SimpleTypeName(value.FieldText("constructor"));
                if (type is null && value?.Type == "identifier" && _imports.TryGetValue(value.Text, out var imported)) type = ImportReceiver(imported);
                if (!string.IsNullOrEmpty(type)) context.Fields[fieldName] = type.Contains("::") ? type : ParserText.SimpleTypeName(type);
            }
            else if (member.Type == "method_definition" && member.FieldText("name") == "constructor")
            {
                CollectConstructorFields(member, context);
            }
        }

        var pendingDecorators = new List<Node>();
        foreach (var member in body.NamedChildren)
        {
            if (member.Type == "decorator")
            {
                pendingDecorators.Add(member);
                continue;
            }

            if (member.Type is "method_definition" or "abstract_method_signature" or "method_signature")
            {
                var methodName = member.FieldText("name");
                var methodDecorators = pendingDecorators.Concat(member.NamedChildren.Where(c => c.Type == "decorator")).ToList();
                pendingDecorators = [];
                if (string.IsNullOrEmpty(methodName) || methodName == "constructor") continue;

                string? verb = null, route = null;
                foreach (var decorator in methodDecorators)
                {
                    var (decoratorName, argument) = DecoratorCall(decorator);
                    if (decoratorName is not null && NestVerbs.Contains(decoratorName))
                    {
                        verb = decoratorName == "All" ? "ANY" : decoratorName.ToUpperInvariant();
                        route = argument;
                    }
                }

                EmitMethod(member, member, name, methodName, context,
                    verb, verb is null ? null : ParserText.CombineRoute(parsedClass.RoutePrefix, route));
                continue;
            }

            pendingDecorators = [];
        }
    }

    private void CollectConstructorFields(Node constructor, ClassContext context)
    {
        var parameters = constructor.Field("parameters");
        if (parameters is not null)
        {
            foreach (var p in parameters.NamedChildren)
            {
                var isProperty = p.NamedChildren.Any(c => c.Type is "accessibility_modifier" or "readonly" or "override_modifier")
                                 || p.Children.Any(c => c.Type == "readonly");
                if (!isProperty) continue;
                var name = p.FieldText("pattern");
                var type = TypeAnnotation(p.Field("type"));
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(type)) context.Fields[name] = ParserText.SimpleTypeName(type);
            }
        }

        var body = constructor.Field("body");
        if (body is null) return;
        foreach (var assignment in body.Descendants().Where(n => n.Type == "assignment_expression"))
        {
            var left = assignment.Field("left");
            var right = assignment.Field("right");
            if (left?.Type != "member_expression" || left.Field("object")?.Type != "this" || right is null) continue;
            var field = left.FieldText("property");
            if (string.IsNullOrEmpty(field)) continue;

            string? type = null;
            if (right.Type == "new_expression") type = ParserText.SimpleTypeName(right.FieldText("constructor"));
            else if (right.Type == "identifier" && _imports.TryGetValue(right.Text, out var imported)) type = ImportReceiver(imported);
            else if (TryRequirePath(right, out var path)) type = ImportModuleName(path);
            if (!string.IsNullOrEmpty(type)) context.Fields[field] = type.Contains("::") ? type : ParserText.SimpleTypeName(type);
        }
    }

    private ParsedMethod? EmitMethod(Node function, Node source, string className, string methodName, ClassContext? classContext, string? verb, string? route)
    {
        if (!_emittedMethods.Add($"{className}.{methodName}")) return null;

        var parameters = new List<ParsedParameter>();
        var locals = new Dictionary<string, string?>(StringComparer.Ordinal);

        var parameterNodes = function.Field("parameters")?.NamedChildren ?? (IReadOnlyList<Node>)[];
        if (parameterNodes.Count == 0 && function.Field("parameter") is { } single) parameterNodes = [single];

        foreach (var p in parameterNodes)
        {
            if (p.Type == "decorator" || p.Type == "comment") continue;
            var pattern = p.Type is "required_parameter" or "optional_parameter" ? p.Field("pattern") : p;
            if (pattern?.Type == "assignment_pattern") pattern = pattern.Field("left");
            var pName = pattern?.Text ?? p.Text;
            var pType = TypeAnnotation(p.Field("type"));
            parameters.Add(new ParsedParameter(ParserText.OneLine(pName, 60), pType));
            if (pattern?.Type == "identifier") locals[pattern.Text] = pType is null ? null : ParserText.SimpleTypeName(pType);
        }

        var body = function.Field("body");
        var returnType = TypeAnnotation(function.Field("return_type"));

        var method = new ParsedMethod
        {
            ClassName = className,
            MethodName = methodName,
            FilePath = _filePath,
            Language = _language,
            SourceCode = ParserText.Source(source.Text),
            StartLine = source.Line(),
            Parameters = parameters,
            ReturnType = returnType,
            IsAbstract = body is null,
            IsDecision = body is not null && body.Descendants().Any(n => BranchNodes.Contains(n.Type)),
            HttpVerb = verb,
            Route = route
        };

        if (body is not null)
        {
            if (body.Type != "statement_block")
            {
                method.ReturnExpression = ParserText.OneLine(body.Text, 60);
            }
            else
            {
                var firstReturn = body.Descendants(n => !FunctionNodes.Contains(n.Type)).FirstOrDefault(n => n.Type == "return_statement");
                var expr = firstReturn?.NamedChildren.FirstOrDefault();
                if (expr is not null) method.ReturnExpression = ParserText.OneLine(expr.Text, 60);
            }

            foreach (var node in body.Descendants())
            {
                if (node.Type == "variable_declarator") RegisterLocal(node, locals);
                if (node.Type != "call_expression") continue;
                var call = ResolveCall(node, body, className, classContext, locals);
                if (call is not null) method.Calls.Add(call);
            }
        }

        _result.Methods.Add(method);
        return method;
    }

    private void RegisterLocal(Node declarator, Dictionary<string, string?> locals)
    {
        var name = declarator.Field("name");
        if (name?.Type != "identifier") return;
        var type = TypeAnnotation(declarator.Field("type"));
        var value = declarator.Field("value");
        if (type is null && value is not null)
        {
            var inner = value.Type == "await_expression" ? value.NamedChildren.FirstOrDefault() : value;
            if (inner?.Type == "new_expression") type = inner.FieldText("constructor");
            else if (inner is not null && TryRequirePath(inner, out var path)) type = ImportModuleName(path);
        }
        locals[name.Text] = type is null ? null : ParserText.SimpleTypeName(type);
    }

    private ParsedCall? ResolveCall(Node call, Node body, string className, ClassContext? classContext, Dictionary<string, string?> locals)
    {
        var function = call.Field("function");
        if (function is null) return null;
        var argCount = call.Field("arguments")?.NamedChildren.Count ?? -1;
        var condition = FindCondition(call, body);

        if (function.Type == "identifier")
        {
            var name = function.Text;
            if (locals.ContainsKey(name)) return null;
            if (_moduleFunctionNames.Contains(name))
                return new ParsedCall { ReceiverType = _module, MethodName = name, Condition = condition, ArgumentCount = argCount };
            if (_imports.TryGetValue(name, out var imported) && imported.Export is not null)
                return new ParsedCall { ReceiverType = imported.Module, MethodName = imported.Export, Condition = condition, ArgumentCount = argCount };
            return null;
        }

        if (function.Type != "member_expression") return null;
        var property = function.FieldText("property");
        var obj = function.Field("object");
        if (string.IsNullOrEmpty(property) || obj is null) return null;

        string? receiver = null;
        switch (obj.Type)
        {
            case "this":
                receiver = className;
                break;
            case "super":
                receiver = classContext?.BaseClass;
                break;
            case "member_expression" when obj.Field("object")?.Type == "this":
            {
                var field = obj.FieldText("property");
                if (field is not null && classContext is not null && classContext.Fields.TryGetValue(field, out var fieldType)) receiver = fieldType;
                break;
            }
            case "identifier":
            {
                var name = obj.Text;
                if (locals.TryGetValue(name, out var localType)) receiver = localType;
                else if (_imports.TryGetValue(name, out var imported))
                    receiver = ImportReceiver(imported);
                else if (_moduleInstances.TryGetValue(name, out var instanceType)) receiver = instanceType;
                else if (_classNames.Contains(name)) receiver = name;
                break;
            }
        }

        return string.IsNullOrEmpty(receiver)
            ? null
            : new ParsedCall { ReceiverType = receiver, MethodName = property, Condition = condition, ArgumentCount = argCount };
    }

    private void CollectRoutes(Node root)
    {
        foreach (var call in root.Descendants().Where(n => n.Type == "call_expression"))
        {
            var function = call.Field("function");
            if (function?.Type != "member_expression") continue;
            var verb = function.FieldText("property");
            if (verb is null || !RouteVerbs.Contains(verb)) continue;

            var args = call.Field("arguments")?.NamedChildren.Where(a => a.Type != "comment").ToList() ?? [];
            var obj = function.Field("object");
            string? path = null;
            var chained = false;
            for (var cursor = obj; cursor?.Type == "call_expression" && cursor.Field("function")?.Type == "member_expression";)
            {
                var member = cursor.Field("function")!;
                if (member.FieldText("property") == "route")
                {
                    path = ParserText.StringLiteralValue(cursor.Field("arguments")?.NamedChildren.FirstOrDefault()?.Text);
                    chained = true;
                    break;
                }
                cursor = member.Field("object");
            }

            if (chained)
            {
                if (args.Count < 1) continue;
            }
            else
            {
                if (obj?.Type != "identifier" || !LooksLikeRouter(obj.Text) || args.Count < 2) continue;
                path = ParserText.StringLiteralValue(args[0].Text);
            }

            if (path is null) continue;
            var routeVerb = verb == "all" ? "ANY" : verb.ToUpperInvariant();
            var handler = args[^1];

            if (handler.Type == "call_expression" && UnwrapFunction(handler) is null)
                handler = handler.Field("arguments")?.NamedChildren.FirstOrDefault() ?? handler;

            if (UnwrapFunction(handler) is { } inline)
            {
                var methodName = $"{routeVerb} {path}";
                var statement = call.Parent?.Type == "expression_statement" ? call.Parent : call;
                EmitMethod(inline, statement, _module, methodName, null, routeVerb, path);
                if (!_result.Classes.Any(c => c.Name == _module && c.FilePath == _filePath))
                    _result.Classes.Add(new ParsedClass { Name = _module, Kind = "module", FilePath = _filePath, Language = _language });
                continue;
            }

            var target = HandlerTarget(handler);
            if (target is not null) _routes.Add(new JsTsTreeSitterParser.JsRouteBinding(routeVerb, path, target.Value.Class, target.Value.Method));
        }
    }

    private (string Class, string Method)? HandlerTarget(Node handler)
    {
        if (handler.Type == "identifier")
        {
            var name = handler.Text;
            if (_moduleFunctionNames.Contains(name)) return (_module, name);
            if (_imports.TryGetValue(name, out var imported) && imported.Export is not null) return (imported.Module, imported.Export);
            return null;
        }

        if (handler.Type == "member_expression")
        {
            var obj = handler.Field("object");
            var property = handler.FieldText("property");
            if (obj?.Type == "identifier" && property is not null)
            {
                if (_imports.TryGetValue(obj.Text, out var imported))
                    return (ImportReceiver(imported), property);
                if (_moduleInstances.TryGetValue(obj.Text, out var type)) return (type, property);
                if (_classNames.Contains(obj.Text)) return (obj.Text, property);
            }
        }

        return null;
    }

    private static bool LooksLikeRouter(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("app") || lower.Contains("router") || lower.Contains("route") || lower.Contains("server") || lower == "r";
    }

    private static (string? Name, string? Argument) DecoratorCall(Node decorator)
    {
        var expression = decorator.NamedChildren.FirstOrDefault();
        if (expression is null) return (null, null);
        if (expression.Type == "identifier") return (expression.Text, null);
        if (expression.Type != "call_expression") return (null, null);

        var name = ParserText.SimpleTypeName(expression.FieldText("function"));
        var argument = ParserText.StringLiteralValue(expression.Field("arguments")?.NamedChildren.FirstOrDefault()?.Text);
        return (name, argument);
    }

    private static string? TypeAnnotation(Node? annotation)
    {
        if (annotation is null) return null;
        var text = annotation.Text.Trim();
        if (text.StartsWith(':')) text = text[1..].Trim();
        return text.Length == 0 ? null : ParserText.OneLine(text, 120);
    }

    private static string? FindCondition(Node node, Node stopAt)
    {
        var child = node;
        for (var current = node.Parent; current is not null && current != stopAt; child = current, current = current.Parent)
        {
            if (FunctionNodes.Contains(current.Type) && current.Parent?.Type == "arguments") continue;

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
                case "switch_case":
                    return ParserText.OneLine($"case {current.FieldText("value")}");
                case "switch_default":
                    return "default";
                case "catch_clause":
                    return ParserText.OneLine($"catch {current.FieldText("parameter")}");
                case "for_statement":
                case "for_in_statement":
                case "while_statement":
                {
                    var loopBody = current.Field("body");
                    if (loopBody is null || !child.IsWithin(loopBody)) break;
                    return ParserText.OneLine(current.Text[..Math.Max(0, loopBody.StartIndex - current.StartIndex)]);
                }
            }
        }
        return null;
    }
}
