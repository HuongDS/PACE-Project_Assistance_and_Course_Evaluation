using PACE.BLL.Dtos;
using PACE.BLL.Exceptions;
using PACE.BLL.Mappings;
using PACE.BLL.Services.Interfaces;
using PACE.DAL.Repositories.Interfaces;

namespace PACE.BLL.Services.Implementations;

public class BusinessService(IUnitOfWork uow, IBusinessFlowService flowService) : IBusinessService
{
    public async Task<IReadOnlyList<BusinessDto>> GetByRunAsync(Guid analysisRunId, CancellationToken ct = default)
        => (await uow.Businesses.GetSummariesByRepoAsync(analysisRunId, ct)).Select(b => b.ToDto()).ToList();

    public async Task<BusinessDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var business = await uow.Businesses.GetSummaryAsync(id, ct) ?? throw new NotFoundException("nghiệp vụ", id);
        var flows = await flowService.GetByBusinessAsync(id, ct);
        return new BusinessDetailDto(business.ToDto(), flows);
    }

    public async Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request, CancellationToken ct = default)
    {
        var business = await uow.Businesses.GetByIdAsync(id, ct) ?? throw new NotFoundException("nghiệp vụ", id);
        var name = request.BusinessName.Trim();

        if (await uow.Businesses.NameExistsAsync(business.AnalysisRunId, name, id, ct))
            throw new ConflictException($"Repo đã có nghiệp vụ tên \"{name}\".");

        business.BusinessName = name;
        await uow.SaveChangesAsync(ct);
        return (await GetDetailAsync(id, ct)).Business;
    }
}
