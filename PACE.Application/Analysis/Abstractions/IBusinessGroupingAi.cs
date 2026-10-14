using PACE.Application.Analysis.Template;

namespace PACE.Application.Analysis.Abstractions;

public interface IBusinessGroupingAi
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<SuggestedBusiness>> GroupAsync(IReadOnlyList<TemplateEndpoint> endpoints, CancellationToken ct);
}
