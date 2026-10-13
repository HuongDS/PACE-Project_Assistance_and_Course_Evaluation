using PACE.BLL.Dtos;
using PACE.BLL.Exceptions;
using PACE.BLL.Mappings;
using PACE.BLL.Services.Interfaces;
using PACE.DAL.Repositories;
using PACE.DAL.Repositories.Interfaces;

namespace PACE.BLL.Services.Implementations;

public class MethodSourceService(IUnitOfWork uow) : IMethodSourceService
{
    public async Task<PagedResult<MethodSourceSummaryDto>> GetPagedAsync(MethodSourceQuery query, CancellationToken ct = default)
    {
        var page = await uow.MethodSources.SearchSummariesAsync(
            query.AnalysisRunId, query.BusinessId, query.Language, query.Search, query.Page, query.PageSize, ct);
        return page.Map(m => m.ToDto());
    }

    public async Task<MethodSourceDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var method = await uow.MethodSources.GetWithBusinessesAsync(id, ct) ?? throw new NotFoundException("hàm", id);
        return new MethodSourceDto(method.ToSummaryDto(), method.SourceCode, method.BusinessMethods.Select(b => b.BusinessId).ToList());
    }
}
