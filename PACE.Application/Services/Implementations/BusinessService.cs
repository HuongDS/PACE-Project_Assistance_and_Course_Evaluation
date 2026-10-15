using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Dtos.StudentAnswer;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.Services.Interfaces;

namespace PACE.Application.Services.Implementations;

public class BusinessService(IUnitOfWork uow, IBusinessFlowService flowService) : IBusinessService
{
    public async Task<IReadOnlyList<BusinessDto>> GetByRunAsync(Guid analysisRunId, CancellationToken ct = default)
        => (await uow.Businesses.GetSummariesByRepoAsync(analysisRunId, ct)).Select(b => b.ToDto()).ToList();

    public async Task<BusinessDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var business = await uow.Businesses.GetSummaryAsync(id, ct) ?? throw new NotFoundException("nghi?p v?", id);
        var flows = await flowService.GetByBusinessAsync(id, ct);
        return new BusinessDetailDto(business.ToDto(), flows);
    }

    public async Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request, CancellationToken ct = default)
    {
        var business = await uow.Businesses.GetByIdAsync(id, ct) ?? throw new NotFoundException("nghi?p v?", id);
        var name = request.BusinessName.Trim();

        if (await uow.Businesses.NameExistsAsync(business.AnalysisRunId, name, id, ct))
            throw new ConflictException($"Repo dã có nghi?p v? tên \"{name}\".");

        business.BusinessName = name;
        await uow.SaveChangesAsync(ct);
        return (await GetDetailAsync(id, ct)).Business;
    }

    public async Task<CodeFlowDto?> GetCodeBaseWithBusinessAsync(Guid businessId, CancellationToken ct = default)
    {
        var business = await uow.Businesses.GetByIdAsync(businessId, ct);

        if (business == null) return null;

        var businessMethods = await uow.BusinessMethods.GetByBusinessMethodsIdWithMethodSourceAsync(businessId);

        var methods = businessMethods
            .Where(m => m.MethodSource != null)
            .Select(m => m.MethodSource!)
            .DistinctBy(m => m.Id)
            .ToList();

        var businessSummary = await uow.Businesses.GetSummaryAsync(businessId, ct);
        var businessDto = businessSummary?.ToDto()
            ?? new BusinessDto(business.Id, business.AnalysisRunId, business.BusinessName, businessMethods.Count, businessSummary.FlowCount);

        var methodsDto = methods
            .Select(m => new MethodSourceDto(m.ToSummaryDto(), m.SourceCode, [businessId]))
            .ToList();

        return new CodeFlowDto
        {
            Business = businessDto,
            Methods = methodsDto
        };
    }
}
