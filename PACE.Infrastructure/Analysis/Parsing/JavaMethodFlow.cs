using PACE.Application.Analysis.Common;
using PACE.Application.Analysis.Models;
using TreeSitter;

namespace PACE.Infrastructure.Analysis.Parsing;

internal sealed class JavaMethodFlow
{
    private const int MaxNodes = 256;
    private const int MaxDepth = 64;
    private const int MaxFacts = 4096;

    private sealed record Variable(int Id, string Name);
    private sealed record Access(Variable Variable, CallSite Site);
    private sealed record ComingFrom(int Node, MethodFlowEdgeKind Kind = MethodFlowEdgeKind.Next);
    private sealed record Loop(int Header, List<ComingFrom> Breaks);
    private sealed record Operation(Variable? Kill, Access? Write, IReadOnlyList<Access> Reads);
    private sealed class Incomplete(MethodFlowStatus status, string reason) : Exception(reason)
    {
        public MethodFlowStatus Status { get; } = status;
    }

    private readonly List<MethodFlowNode> nodes = [];
    private readonly List<MethodFlowEdge> edges = [];
    private readonly Dictionary<int, Operation> operations = [];
    private readonly List<Access> parameters = [];
    private int nextVariable;
    private int exit;

    public static MethodFlowAnalysis Analyze(Node method, Node body)
    {
        try
        {
            if (method.HasError) throw Unsupported("invalid-syntax");
            if (body.EndIndex - body.StartIndex > ParserText.MaxSourceLength) throw Limit("source-length");
            return new JavaMethodFlow().Build(method, body);
        }
        catch (Incomplete stop)
        {
            return new MethodFlowAnalysis(stop.Status, stop.Message, [], [], []);
        }
    }

    private MethodFlowAnalysis Build(Node method, Node body)
    {
        var scope = new Dictionary<string, Variable>(StringComparer.Ordinal);
        if (method.Field("parameters") is { } list)
        {
            foreach (var parameter in Children(list).Where(node => node.Type != "receiver_parameter"))
            {
                var name = parameter.Field("name") ??
                    Children(parameter).FirstOrDefault(node => node.Type == "variable_declarator")?.Field("name");
                if (name is null || name.Type != "identifier") throw Unsupported(parameter.Type);
                parameters.Add(Declare(name, scope));
            }
        }

        var entry = AddNode(MethodFlowNodeKind.Entry, method.CallSite(), []);
        exit = AddNode(MethodFlowNodeKind.Exit, new CallSite(body.EndPosition.Row + 1, body.EndPosition.Column + 1), []);
        Connect(Walk(body, [new ComingFrom(entry)], scope, null, 0), exit);
        return new MethodFlowAnalysis(MethodFlowStatus.Complete, null, nodes, edges, ReachingDefinitions(entry));
    }

    private List<ComingFrom> Walk(Node statement, List<ComingFrom> incoming, Dictionary<string, Variable> scope,
        Loop? loop, int depth)
    {
        CheckDepth(depth);
        switch (statement.Type)
        {
            case "block":
                var local = new Dictionary<string, Variable>(scope, StringComparer.Ordinal);
                foreach (var child in Children(statement))
                    incoming = Walk(child, incoming, local, loop, depth + 1);
                return incoming;
            case "empty_statement":
            case ";":
                return incoming;
            case "local_variable_declaration":
                foreach (var declaration in Children(statement).Where(node => node.Type == "variable_declarator"))
                {
                    var write = Declare(Required(declaration, "name"), scope);
                    var value = declaration.Field("value");
                    var id = AddNode(MethodFlowNodeKind.Statement, write.Site, incoming, Read(value, scope),
                        value is null ? null : write, kill: write.Variable);
                    incoming = [new ComingFrom(id)];
                }
                return incoming;
            case "expression_statement":
                return [new ComingFrom(AddExpressionStatement(statement, incoming, scope))];
            case "if_statement":
            {
                var condition = Required(statement, "condition");
                var test = Condition(condition, incoming, scope);
                var constant = ConstantBoolean(condition);
                var whenTrue = Branch(Required(statement, "consequence"), conditionValue: true, enabled: constant != false);
                var alternative = statement.Field("alternative");
                if (alternative is not null)
                    whenTrue.AddRange(Branch(alternative, conditionValue: false, enabled: constant != true));
                else if (constant != true)
                    whenTrue.Add(new ComingFrom(test, MethodFlowEdgeKind.False));
                return whenTrue;

                List<ComingFrom> Branch(Node branch, bool conditionValue, bool enabled) => Walk(branch,
                    enabled ? [new ComingFrom(test, conditionValue ? MethodFlowEdgeKind.True : MethodFlowEdgeKind.False)] : [],
                    new Dictionary<string, Variable>(scope, StringComparer.Ordinal), loop, depth + 1);
            }
            case "while_statement":
            {
                var condition = Required(statement, "condition");
                var header = Condition(condition, incoming, scope);
                var constant = ConstantBoolean(condition);
                var current = new Loop(header, []);
                Connect(Walk(Required(statement, "body"),
                    constant == false ? [] : [new ComingFrom(header, MethodFlowEdgeKind.True)],
                    new Dictionary<string, Variable>(scope, StringComparer.Ordinal), current, depth + 1), header);
                if (constant != true)
                    current.Breaks.Add(new ComingFrom(header, MethodFlowEdgeKind.False));
                return current.Breaks;
            }
            case "return_statement":
            case "throw_statement":
            {
                var isReturn = statement.Type == "return_statement";
                var id = AddNode(isReturn ? MethodFlowNodeKind.Return : MethodFlowNodeKind.Throw,
                    statement.CallSite(), incoming, Read(OnlyChild(statement), scope));
                Connect([new ComingFrom(id, isReturn ? MethodFlowEdgeKind.Return : MethodFlowEdgeKind.Throw)], exit);
                return [];
            }
            case "break_statement":
            case "continue_statement":
            {
                if (loop is null || Children(statement).Any()) throw Unsupported(statement.Type);
                var isBreak = statement.Type == "break_statement";
                var id = AddNode(isBreak ? MethodFlowNodeKind.Break : MethodFlowNodeKind.Continue,
                    statement.CallSite(), incoming);
                if (isBreak) loop.Breaks.Add(new ComingFrom(id, MethodFlowEdgeKind.Break));
                else Connect([new ComingFrom(id, MethodFlowEdgeKind.Continue)], loop.Header);
                return [];
            }
            default:
                throw Unsupported(statement.Type);
        }
    }

    private int AddExpressionStatement(Node statement, List<ComingFrom> incoming, Dictionary<string, Variable> scope)
    {
        var expression = OnlyChild(statement) ?? throw Unsupported(statement.Type);
        var reads = new List<Access>();
        Access? write = null;
        if (expression.Type is "assignment_expression" or "update_expression")
        {
            var update = expression.Type == "update_expression";
            var target = update ? OnlyChild(expression) ?? throw Unsupported(expression.Type) : Required(expression, "left");
            var isLocal = target.Type == "identifier" && scope.ContainsKey(target.Text);
            if (isLocal)
                write = new Access(scope[target.Text], target.CallSite());
            if (!isLocal || update || expression.FieldText("operator") != "=")
                reads.AddRange(Read(target, scope));
            if (!update) reads.AddRange(Read(Required(expression, "right"), scope));
        }
        else
        {
            reads.AddRange(Read(expression, scope));
        }
        return AddNode(MethodFlowNodeKind.Statement, statement.CallSite(), incoming, reads, write);
    }

    private int Condition(Node expression, List<ComingFrom> incoming, Dictionary<string, Variable> scope) =>
        AddNode(MethodFlowNodeKind.Condition, expression.CallSite(), incoming, Read(expression, scope),
            condition: ParserText.OneLine(ParserText.StripParens(expression.Text)));

    private static bool? ConstantBoolean(Node expression)
    {
        while (expression.Type == "parenthesized_expression")
            expression = OnlyChild(expression) ?? throw Unsupported(expression.Type);
        return expression.Type switch { "true" => true, "false" => false, _ => null };
    }

    private static List<Access> Read(Node? expression, Dictionary<string, Variable> scope)
    {
        var reads = new List<Access>();
        Visit(expression, 0);
        return reads;

        void Visit(Node? node, int depth)
        {
            if (node is null) return;
            CheckDepth(depth);
            if (reads.Count >= MaxFacts) throw Limit("local-uses");
            switch (node.Type)
            {
                case "identifier":
                    if (scope.TryGetValue(node.Text, out var local)) reads.Add(new Access(local, node.CallSite()));
                    return;
                case "method_invocation":
                    Visit(node.Field("object"), depth + 1);
                    Visit(Required(node, "arguments"), depth + 1);
                    return;
                case "field_access":
                    Visit(Required(node, "object"), depth + 1);
                    return;
                case "cast_expression":
                    Visit(Required(node, "value"), depth + 1);
                    return;
                case "object_creation_expression":
                    foreach (var child in Children(node).TakeWhile(child => child.Type != "argument_list"))
                    {
                        if (child.IsWithin(node.Field("type")) || child.Type == "type_arguments") continue;
                        Visit(child, depth + 1);
                    }
                    Visit(Required(node, "arguments"), depth + 1);
                    return;
                case "array_access":
                case "argument_list":
                case "parenthesized_expression":
                case "unary_expression":
                case "binary_expression":
                case "ternary_expression":
                    foreach (var child in Children(node)) Visit(child, depth + 1);
                    return;
                case "this":
                case "super":
                case "class_literal":
                case "true":
                case "false":
                case "null_literal":
                    return;
                default:
                    if (node.Type.EndsWith("_literal", StringComparison.Ordinal)) return;
                    throw Unsupported(node.Type);
            }
        }
    }

    private IReadOnlyList<MethodDefUse> ReachingDefinitions(int entry)
    {
        var successors = edges.ToLookup(edge => edge.From, edge => edge.To);
        var incoming = new HashSet<Access>?[nodes.Count];
        incoming[entry] = [.. parameters];
        var pending = new Queue<int>();
        var queued = new bool[nodes.Count];
        pending.Enqueue(entry);
        queued[entry] = true;

        while (pending.TryDequeue(out var id))
        {
            queued[id] = false;
            var available = incoming[id]!;
            var operation = operations[id];
            if (operation.Kill is { } kill)
            {
                // A declaration also clears a previous iteration's value before it is initialized.
                available = available.Where(prior => prior.Variable != kill).ToHashSet();
                if (operation.Write is { } write) available.Add(write);
            }
            foreach (var next in successors[id])
            {
                var state = incoming[next];
                var changed = state is null;
                state ??= incoming[next] = [];
                var before = state.Count;
                state.UnionWith(available);
                if ((changed || state.Count != before) && !queued[next])
                {
                    queued[next] = true;
                    pending.Enqueue(next);
                }
            }
        }

        var facts = new HashSet<MethodDefUse>();
        foreach (var node in nodes)
        {
            if (incoming[node.Id] is not { } definitions) continue;
            foreach (var use in operations[node.Id].Reads)
            foreach (var definition in definitions.Where(definition => definition.Variable == use.Variable))
            {
                facts.Add(new MethodDefUse(use.Variable.Name, definition.Site, use.Site));
                if (facts.Count > MaxFacts) throw Limit("def-use");
            }
        }
        return facts.OrderBy(fact => fact.Use.Line).ThenBy(fact => fact.Use.Column)
            .ThenBy(fact => fact.Definition.Line).ThenBy(fact => fact.Definition.Column).ToArray();
    }

    private Access Declare(Node name, Dictionary<string, Variable> scope)
    {
        var variable = new Variable(nextVariable++, name.Text);
        if (!scope.TryAdd(name.Text, variable)) throw Unsupported("duplicate-local");
        return new Access(variable, name.CallSite());
    }

    private int AddNode(MethodFlowNodeKind kind, CallSite site, List<ComingFrom> incoming,
        IReadOnlyList<Access>? reads = null, Access? write = null, string? condition = null, Variable? kill = null)
    {
        if (nodes.Count >= MaxNodes) throw Limit("cfg-nodes");
        var id = nodes.Count;
        nodes.Add(new MethodFlowNode(id, kind, site, condition));
        operations.Add(id, new Operation(kill ?? write?.Variable, write, reads ?? []));
        Connect(incoming, id);
        return id;
    }

    private void Connect(IEnumerable<ComingFrom> incoming, int to)
    {
        edges.AddRange(incoming.Distinct().Select(link => new MethodFlowEdge(link.Node, to, link.Kind)));
    }

    private static IEnumerable<Node> Children(Node node) =>
        node.NamedChildren.Where(child => child.Type is not ("line_comment" or "block_comment"));

    private static Node? OnlyChild(Node node)
    {
        using var children = Children(node).GetEnumerator();
        if (!children.MoveNext()) return null;
        var child = children.Current;
        return children.MoveNext() ? throw Unsupported(node.Type) : child;
    }

    private static Node Required(Node node, string field) => node.Field(field) ?? throw Unsupported(node.Type);

    private static void CheckDepth(int depth)
    {
        if (depth > MaxDepth) throw Limit("syntax-depth");
    }

    private static Incomplete Unsupported(string reason) => new(MethodFlowStatus.Unsupported, reason);
    private static Incomplete Limit(string reason) => new(MethodFlowStatus.LimitReached, reason);
}
