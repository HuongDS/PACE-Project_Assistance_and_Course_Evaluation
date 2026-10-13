using PACE.BLL.Analysis;
using PACE.BLL.Common;
using PACE.BLL.Dtos;
using PACE.BLL.Exceptions;
using PACE.BLL.Mappings;
using PACE.BLL.Services.Interfaces;
using PACE.DAL.Models;
using PACE.DAL.Repositories;
using PACE.DAL.Repositories.Interfaces;

namespace PACE.BLL.Services.Implementations;

public class AnalysisRunService(IUnitOfWork uow, ICurrentUserService currentUser, IBusinessService businessService) : IAnalysisRunService
{
    public async Task<PagedResult<AnalysisRunDto>> GetPagedAsync(AnalysisRunQuery query, CancellationToken ct = default)
    {
        var page = await uow.AnalysisRuns.SearchSummariesAsync(query.Search, query.Page, query.PageSize, ct);
        return page.Map(r => r.ToDto(AnalysisRunLock.IsRunning(r.Id)));
    }

    public async Task<AnalysisRunDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var summary = await uow.AnalysisRuns.GetSummaryAsync(id, ct) ?? throw new NotFoundException("repo", id);
        var businesses = await businessService.GetByRunAsync(id, ct);
        var emails = await uow.Users.GetEmailsByRepoAsync(id, ct);
        return new AnalysisRunDetailDto(summary.ToDto(AnalysisRunLock.IsRunning(id)), businesses, emails);
    }

    public Task<AnalysisRunDetailDto> GetMineAsync(CancellationToken ct = default)
    {
        if (currentUser.AnalysisRunId is not { } runId)
            throw new NotFoundException("Tài khoản của bạn chưa được gán repo nào.");
        return GetDetailAsync(runId, ct);
    }

    public async Task<AnalysisRunDto> UpdateAsync(Guid id, UpdateAnalysisRunRequest request, CancellationToken ct = default)
    {
        var run = await uow.AnalysisRuns.GetByIdAsync(id, ct) ?? throw new NotFoundException("repo", id);
        var url = RepoUrlHelper.Normalize(request.RepoUrl);
        await EnsureUrlAvailableAsync(url, id, ct);

        run.RepoUrl = url;
        run.RepoName = string.IsNullOrWhiteSpace(request.RepoName) ? RepoUrlHelper.DeriveName(url) : request.RepoName.Trim();
        await uow.SaveChangesAsync(ct);
        return (await GetDetailAsync(id, ct)).Run;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var run = await uow.AnalysisRuns.GetByIdAsync(id, ct) ?? throw new NotFoundException("repo", id);
        if (AnalysisRunLock.IsRunning(id))
            throw new ConflictException("Repo đang được phân tích, không thể xoá lúc này.");
        if (currentUser.AnalysisRunId == id)
            throw new BadRequestException("Không thể xoá repo mà tài khoản đang đăng nhập thuộc về.");

        uow.AnalysisRuns.Remove(run);
        await uow.SaveChangesAsync(ct);
    }

    private async Task EnsureUrlAvailableAsync(string normalizedUrl, Guid? exceptId, CancellationToken ct)
    {
        if (await uow.AnalysisRuns.RepoUrlExistsAsync(normalizedUrl, exceptId, ct))
            throw new ConflictException($"Repo {normalizedUrl} đã tồn tại.");
    }
}
