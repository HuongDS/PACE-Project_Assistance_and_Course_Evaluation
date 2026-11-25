using System.Text.RegularExpressions;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Domain.Enums;

namespace PACE.Application.Analysis.Flow;

public sealed partial class EntryPointMatcher(IReadOnlyCollection<ParsedMethod> methods)
{
    private readonly ILookup<string, ParsedMethod> _byClass = methods.Where(m => m.Language != ProgrammingLanguage.Java)
        .ToLookup(m => NormalizeClass(m.ClassName));
    private readonly ILookup<string, (ParsedMethod Method, EndpointMapping Endpoint)> _springByRoute = methods
        .SelectMany(method => method.SpringEndpoints.Select(endpoint => (Method: method, Endpoint: endpoint)))
        .ToLookup(item => NormalizeSpringPattern(item.Endpoint.Path), StringComparer.Ordinal);
    private readonly ILookup<string, ParsedMethod> _javaByFunction = methods
        .Where(method => method.Language == ProgrammingLanguage.Java && !method.IsAbstract)
        .ToLookup(method => method.MethodName, StringComparer.Ordinal);

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"(\{[^}]*\}|:[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex RouteParameter();

    [GeneratedRegex(@"\{(\*?)[\p{L}\p{Nd}_]+((?::(?:[^{}]|\{[^{}]*\})+)?)\}")]
    private static partial Regex SpringVariable();

    public ParsedMethod? Match(ApiTemplate api, out string? note)
    {
        note = null;
        if (api.Controller.StartsWith(RepositoryScan.FrontendClassPrefix, StringComparison.Ordinal))
            return methods.FirstOrDefault(m => m.ClassName == api.Controller && m.MethodName == api.ResolveFunctionName());

        var spring = MatchSpring(api, out var foundSpring, out note);
        if (foundSpring) return spring;

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

        if (candidates.Count > 1 && !string.IsNullOrWhiteSpace(api.Path))
        {
            var path = NormalizeRoute(api.Path);
            var samePath = candidates.Where(m => m.Route is not null && NormalizeRoute(m.Route) == path).ToList();
            if (samePath.Count > 0) candidates = samePath;
        }

        var concrete = candidates.Where(m => !m.IsAbstract).ToList();
        if (concrete.Count > 0) candidates = concrete;

        if (candidates.Count > 1)
            note = $"API {api.DisplayName} khớp {candidates.Count} hàm, chọn {candidates[0].Label} ({candidates[0].FilePath}).";

        return candidates[0];
    }

    private ParsedMethod? MatchSpring(ApiTemplate api, out bool matchedSpring, out string? note)
    {
        note = null;
        var function = api.ResolveFunctionName();
        var controller = api.Controller.Trim();
        var named = _javaByFunction[function].Where(method => MatchesSpringHint(method, controller, function)).ToList();
        if (string.IsNullOrWhiteSpace(api.Path))
        {
            matchedSpring = named.Count > 0;
            if (named.Count > 1) note = $"Handler Java mơ hồ cho {api.DisplayName} ({named.Count} khai báo).";
            return named.Count == 1 ? named[0] : null;
        }

        var path = NormalizeSpringPattern(api.Path);
        var verb = api.ResolveHttpVerb();
        var routes = _springByRoute[path].ToList();
        var compatible = routes.Where(item => verb is null || item.Endpoint.Verb == verb || item.Endpoint.Verb == "ANY").ToList();
        var exactVerb = compatible.Where(item => item.Endpoint.Verb == verb).ToList();
        var candidates = (exactVerb.Count > 0 ? exactVerb : compatible).Select(item => item.Method).Distinct().ToList();
        matchedSpring = routes.Count > 0 || named.Count > 0;
        if (!matchedSpring) return null;

        if (candidates.Count == 1) return candidates[0];
        if (candidates.Count > 1)
        {
            var hinted = candidates.Where(method => MatchesSpringHint(method, controller, function)).ToList();
            if (hinted.Count == 1) return hinted[0];
            note = $"Route Spring {verb ?? "ANY"} {api.Path} có {candidates.Count} handler phù hợp.";
        }
        else
        {
            note = $"Không tìm thấy mapping Spring cho {verb ?? "ANY"} {api.Path}.";
        }
        return null;
    }

    private static bool MatchesSpringHint(ParsedMethod method, string controller, string function)
        => controller.Length > 0 && function.Length > 0 && method.MethodName == function &&
           (method.ClassName == controller || method.JavaOwner == controller);

    private static string NormalizeSpringPattern(string path)
        => "/" + SpringVariable().Replace(path.Trim().TrimStart('/'), "{$1$2}");

    private List<ParsedMethod> MatchByRoute(ApiTemplate api)
    {
        if (string.IsNullOrWhiteSpace(api.Path)) return [];
        var path = NormalizeRoute(api.Path);
        var verb = api.ResolveHttpVerb();

        var withRoutes = methods.Where(m => m.Language != ProgrammingLanguage.Java && m.Route is not null).ToList();
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
