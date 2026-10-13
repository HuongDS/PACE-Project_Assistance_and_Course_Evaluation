using PACE.BLL.Analysis.Models;

namespace PACE.BLL.Analysis.Flow;

public sealed class CallResolver
{
    private readonly Dictionary<string, List<ParsedMethod>> _methodsByClass;
    private readonly Dictionary<string, List<ParsedClass>> _classesByName;
    private readonly Dictionary<string, HashSet<string>> _implementations;
    private readonly Dictionary<string, List<string>> _classesByFile;

    public CallResolver(IReadOnlyCollection<ParsedClass> classes, IReadOnlyCollection<ParsedMethod> methods)
    {
        _methodsByClass = methods
            .GroupBy(m => Key(m.ClassName))
            .ToDictionary(g => g.Key, g => g.ToList());

        _classesByName = classes
            .GroupBy(c => Key(c.Name))
            .ToDictionary(g => g.Key, g => g.ToList());

        _classesByFile = classes
            .Where(c => c.Kind != "module")
            .GroupBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Name).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

        _implementations = new Dictionary<string, HashSet<string>>();
        foreach (var c in classes.Where(c => c.Kind is "class" or "record" or "struct"))
        {
            foreach (var baseType in AllBaseTypes(c.Name))
                AddImplementation(baseType, c.Name);
        }

        var names = _methodsByClass.Keys.ToHashSet();
        foreach (var name in names)
        {
            if (name.EndsWith("impl") && name.Length > 4) AddImplementation(name[..^4], name);
            if (name.Length > 1 && name[0] == 'i' && names.Contains(name[1..])) AddImplementation(name, name[1..]);
        }
    }

    public IReadOnlyList<ParsedMethod> Resolve(ParsedCall call, ParsedMethod caller)
    {
        var receiver = string.IsNullOrWhiteSpace(call.ReceiverType) ? caller.ClassName : call.ReceiverType;
        var candidates = CandidateClasses(Key(receiver));

        var found = new List<ParsedMethod>();
        foreach (var className in candidates)
        {
            if (!_methodsByClass.TryGetValue(className, out var methods)) continue;
            var matches = methods.Where(m => string.Equals(m.MethodName, call.MethodName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1 && call.ArgumentCount >= 0)
            {
                var exact = matches.Where(m => m.Parameters.Count == call.ArgumentCount).ToList();
                if (exact.Count > 0) matches = exact;
            }
            found.AddRange(matches);
        }

        found.RemoveAll(m => m.Id == caller.Id || IsTrivialAccessor(m));
        var concrete = found.Where(m => !m.IsAbstract).ToList();
        return (concrete.Count > 0 ? concrete : found).DistinctBy(m => m.Id).ToList();
    }

    public static bool IsTrivialAccessor(ParsedMethod method)
    {
        if (method.IsDecision || method.Calls.Count > 0 || method.HttpVerb is not null) return false;
        var name = method.MethodName;
        var isAccessorName = (name.Length > 3 && (name.StartsWith("get") || name.StartsWith("set")) && char.IsUpper(name[3]))
                             || (name.Length > 2 && name.StartsWith("is") && char.IsUpper(name[2]));
        if (!isAccessorName) return false;
        return method.SourceCode.Count(c => c == '\n') <= 4;
    }

    private IEnumerable<string> CandidateClasses(string receiver)
    {
        var result = new List<string> { receiver };
        var queue = new Queue<string>();
        queue.Enqueue(receiver);
        var visited = new HashSet<string> { receiver };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!_implementations.TryGetValue(current, out var impls)) continue;
            foreach (var impl in impls)
            {
                if (!visited.Add(impl)) continue;
                result.Add(impl);
                queue.Enqueue(impl);
            }
        }

        if (_classesByName.TryGetValue(receiver, out var declared))
        {
            foreach (var module in declared.Where(c => c.Kind == "module"))
            {
                if (!_classesByFile.TryGetValue(module.FilePath, out var siblings)) continue;
                foreach (var sibling in siblings.Select(Key))
                    if (visited.Add(sibling)) result.Add(sibling);
            }
        }

        foreach (var baseType in AllBaseTypes(receiver))
            if (visited.Add(baseType)) result.Add(baseType);

        return result;
    }

    private IEnumerable<string> AllBaseTypes(string className)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(Key(className));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!_classesByName.TryGetValue(current, out var classes)) continue;
            foreach (var baseType in classes.SelectMany(c => c.BaseTypes).Select(Key))
            {
                if (baseType.Length == 0 || !visited.Add(baseType)) continue;
                yield return baseType;
                queue.Enqueue(baseType);
            }
        }
    }

    private void AddImplementation(string abstraction, string implementation)
    {
        var a = Key(abstraction);
        var i = Key(implementation);
        if (a == i) return;
        if (!_implementations.TryGetValue(a, out var set)) _implementations[a] = set = [];
        set.Add(i);
    }

    private static string Key(string name) => name.Trim().ToLowerInvariant();
}
