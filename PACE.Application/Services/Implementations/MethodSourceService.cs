using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.Services.Interfaces;

namespace PACE.Application.Services.Implementations;

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
