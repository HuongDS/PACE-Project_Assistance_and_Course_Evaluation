using PACE.BLL.Dtos;
using PACE.BLL.Exceptions;
using PACE.BLL.Mappings;
using PACE.BLL.Services.Interfaces;
using PACE.DAL.Repositories.Interfaces;

namespace PACE.BLL.Services.Implementations;

public class BusinessFlowService(IUnitOfWork uow) : IBusinessFlowService
{
    public async Task<IReadOnlyList<BusinessFlowSummaryDto>> GetByBusinessAsync(Guid businessId, CancellationToken ct = default)
    {
        if (!await uow.Businesses.AnyAsync(b => b.Id == businessId, ct)) throw new NotFoundException("nghiệp vụ", businessId);
        return (await uow.BusinessFlows.GetSummariesByBusinessAsync(businessId, ct)).Select(f => f.ToDto()).ToList();
    }

    public async Task<BusinessFlowDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var summary = await uow.BusinessFlows.GetSummaryAsync(id, ct) ?? throw new NotFoundException("luồng nghiệp vụ", id);
        var mermaid = await uow.BusinessFlows.GetMermaidGraphAsync(id, ct);
        var edges = await uow.DataFlowEdges.GetDetailsByFlowAsync(id, ct);
        return new BusinessFlowDetailDto(summary.ToDto(), mermaid, edges.Select(e => e.ToDto()).ToList());
    }
}
