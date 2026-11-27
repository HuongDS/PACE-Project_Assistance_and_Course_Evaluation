using PACE.Application.Analysis.Template;

namespace PACE.Application.Analysis.Abstractions;

public interface IBusinessGroupingAi
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<SuggestedBusiness>> GroupAsync(IReadOnlyList<TemplateEndpoint> endpoints, CancellationToken ct);

    async Task<LegacyGroupingResult> GroupWithUsageAsync(IReadOnlyList<TemplateEndpoint> endpoints, CancellationToken ct)
        => new(await GroupAsync(endpoints, ct), LlmTokenUsage.Empty);

    Task<HybridSynthesisResult> SynthesizeAndGroupAsync(
        IReadOnlyList<EndpointEvidenceSlice> slices,
        CancellationToken ct,
        int? batchSize = null)
        => throw new NotSupportedException();
}
