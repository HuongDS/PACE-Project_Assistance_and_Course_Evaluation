using System.Text.RegularExpressions;
using PACE.BLL.Analysis.Models;
using PACE.BLL.Analysis.Template;

namespace PACE.BLL.Analysis.Flow;

public sealed partial class EntryPointMatcher(IReadOnlyCollection<ParsedMethod> methods)
{
    private readonly ILookup<string, ParsedMethod> _byClass = methods.ToLookup(m => NormalizeClass(m.ClassName));

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"(\{[^}]*\}|:[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex RouteParameter();

    public ParsedMethod? Match(ApiTemplate api, out string? note)
    {
        note = null;
        var function = NormalizeMethod(api.ResolveFunctionName());
        var controller = NormalizeClass(api.Controller);

        var candidates = ClassCandidates(controller)
            .SelectMany(c => _byClass[c])
            .Where(m => NormalizeMethod(m.MethodName) == function)
            .ToList();

        if (candidates.Count == 0) candidates = MatchByRoute(api);

        if (candidates.Count == 0) return null;

        var verb = api.ResolveHttpVerb();
        if (candidates.Count > 1 && verb is not null)
        {
            var sameVerb = candidates.Where(m => string.Equals(m.HttpVerb, verb, StringComparison.OrdinalIgnoreCase)).ToList();
            if (sameVerb.Count > 0) candidates = sameVerb;
        }

        var concrete = candidates.Where(m => !m.IsAbstract).ToList();
        if (concrete.Count > 0) candidates = concrete;

        if (candidates.Count > 1)
            note = $"API {api.DisplayName} khớp {candidates.Count} hàm, chọn {candidates[0].Label} ({candidates[0].FilePath}).";

        return candidates[0];
    }

    private List<ParsedMethod> MatchByRoute(ApiTemplate api)
    {
        if (string.IsNullOrWhiteSpace(api.Path)) return [];
        var path = NormalizeRoute(api.Path);
        var verb = api.ResolveHttpVerb();

        var withRoutes = methods.Where(m => m.Route is not null).ToList();
        var exact = withRoutes.Where(m => NormalizeRoute(m.Route!) == path && VerbMatches(m, verb)).ToList();
        if (exact.Count > 0) return exact;

        return withRoutes
            .Where(m => VerbMatches(m, verb))
            .Where(m =>
            {
                var r = NormalizeRoute(m.Route!);
                return r.Length > 1 && (path.EndsWith(r, StringComparison.Ordinal) || r.EndsWith(path, StringComparison.Ordinal));
            })
            .ToList();
    }

    private static bool VerbMatches(ParsedMethod m, string? verb)
        => verb is null || m.HttpVerb is null or "ANY" || string.Equals(m.HttpVerb, verb, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ClassCandidates(string controller)
    {
        yield return controller;
        if (controller.EndsWith("controller") && controller.Length > "controller".Length)
            yield return controller[..^"controller".Length];
        else
            yield return controller + "controller";
        if (controller.EndsWith("impl")) yield return controller[..^4];
        else yield return controller + "impl";
    }

    private static string NormalizeClass(string name) => NonAlphanumeric().Replace(name.ToLowerInvariant(), string.Empty);

    private static string NormalizeMethod(string name)
    {
        var n = NonAlphanumeric().Replace(name.ToLowerInvariant(), string.Empty);
        return n.Length > 5 && n.EndsWith("async") ? n[..^5] : n;
    }

    private static string NormalizeRoute(string route)
    {
        var r = RouteParameter().Replace(route.Trim().ToLowerInvariant(), "{}");
        var query = r.IndexOf('?');
        if (query >= 0) r = r[..query];
        return "/" + r.Trim('/');
    }
}
