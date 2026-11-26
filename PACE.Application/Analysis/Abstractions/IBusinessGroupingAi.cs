using PACE.Application.Analysis.Template;

namespace PACE.Application.Analysis.Abstractions;

public interface IBusinessGroupingAi
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<SuggestedBusiness>> GroupAsync(IReadOnlyList<TemplateEndpoint> endpoints, CancellationToken ct);

    Task<HybridSynthesisResult> SynthesizeAndGroupAsync(
        IReadOnlyList<EndpointEvidenceSlice> slices,
        CancellationToken ct)
        => throw new NotSupportedException();
}
