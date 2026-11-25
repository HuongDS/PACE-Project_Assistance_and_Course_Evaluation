using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Analysis.Models;

public sealed record SourceFile(string FullPath, string RelativePath, string Content);

public sealed class ParsedClass
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required string FilePath { get; init; }
    public required ProgrammingLanguage Language { get; init; }
    public List<string> BaseTypes { get; } = [];
    public string? RoutePrefix { get; set; }
}

public sealed record ParsedParameter(string Name, string? Type)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Type) ? Name : $"{Type} {Name}";
}

public sealed class ParsedCall
{
    public string? ReceiverType { get; set; }
    public required string MethodName { get; init; }
    public string? Condition { get; init; }
    public int ArgumentCount { get; init; } = -1;
    public JavaCallBinding? JavaBinding { get; set; }
    public CallSite? Site { get; set; }
}

public sealed record CallSite(int Line, int Column);

public sealed record JavaCallBinding(string Declaration, IReadOnlyList<string> SourceTargets);
public sealed record EndpointMapping(string Verb, string Path);

public sealed class ParsedMethod
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string ClassName { get; init; }
    public required string MethodName { get; init; }
    public required string FilePath { get; init; }
    public required ProgrammingLanguage Language { get; init; }
    public required string SourceCode { get; init; }
    public int StartLine { get; init; }
    public List<ParsedParameter> Parameters { get; init; } = [];
    public string? ReturnType { get; init; }
    public string? ReturnExpression { get; set; }
    public bool IsAbstract { get; init; }
    public bool IsDecision { get; init; }
    public string? HttpVerb { get; set; }
    public string? Route { get; set; }
    public List<ParsedCall> Calls { get; } = [];
    public string? JavaSymbol { get; set; }
    public IReadOnlyList<string> JavaOverriddenSymbols { get; set; } = [];
    public List<EndpointMapping> SpringEndpoints { get; } = [];
    public MethodFlowAnalysis? JavaFlow { get; set; }

    public string JavaOwner => JavaSymbol is { } symbol ? symbol[..symbol.IndexOf('#')] : ClassName;

    public string Label => JavaSymbol?.Replace('#', '.') ?? $"{ClassName}.{MethodName}";
}

public sealed class ParseResult
{
    public List<ParsedClass> Classes { get; } = [];
    public List<ParsedMethod> Methods { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool HasSyntaxReadFailure { get; set; }

    public void Merge(ParseResult other)
    {
        Classes.AddRange(other.Classes);
        Methods.AddRange(other.Methods);
        Warnings.AddRange(other.Warnings);
        HasSyntaxReadFailure |= other.HasSyntaxReadFailure;
    }
}
