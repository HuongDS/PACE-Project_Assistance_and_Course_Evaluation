using PACE.Application.Analysis;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

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

    public async Task<AnalysisRunTemplateDto> GetTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var run = await uow.AnalysisRuns.GetByIdAsync(id, ct) ?? throw new NotFoundException("repo", id);
        var businesses = StoredBusinessTemplate.Load(run)
                         ?? throw new NotFoundException("Repo chưa có template nghiệp vụ nào được lưu.");
        return ToTemplateDto(run, businesses);
    }

    public async Task<AnalysisRunTemplateDto> SaveTemplateAsync(Guid id, BusinessTemplateRequest request, CancellationToken ct = default)
    {
        var businesses = BusinessTemplateLoader.Normalize(request.Businesses);
        var run = await uow.AnalysisRuns.GetByIdAsync(id, ct) ?? throw new NotFoundException("repo", id);
        if (AnalysisRunLock.IsRunning(id))
            throw new ConflictException("Repo đang được phân tích, vui lòng đợi xong rồi lưu template.");

        StoredBusinessTemplate.Save(run, businesses, BusinessTemplateSource.Confirmed);
        await uow.SaveChangesAsync(ct);
        return ToTemplateDto(run, businesses);
    }

    private static AnalysisRunTemplateDto ToTemplateDto(AnalysisRun run, IReadOnlyList<BusinessTemplate> businesses)
        => new(run.Id, run.TemplateSource ?? BusinessTemplateSource.Repository, run.TemplateUpdatedAt,
            businesses.Count, businesses.Sum(b => b.Apis.Count), businesses);

    private async Task EnsureUrlAvailableAsync(string normalizedUrl, Guid? exceptId, CancellationToken ct)
    {
        if (await uow.AnalysisRuns.RepoUrlExistsAsync(normalizedUrl, exceptId, ct))
            throw new ConflictException($"Repo {normalizedUrl} đã tồn tại.");
    }

    public async Task<string> GetNextCopyNameAsync(Guid id, CancellationToken ct = default)
    {
        var run = await uow.AnalysisRuns.GetByIdAsync(id, ct) ?? throw new NotFoundException("repo", id);
        var allNames = await uow.AnalysisRuns.GetAllRepoNamesAsync(ct);
        var baseName = run.RepoName ?? (run.RepoUrl != null ? RepoUrlHelper.DeriveName(run.RepoUrl) : "Repository");
        return RepoUrlHelper.ComputeCopyName(baseName, allNames);
    }

    public async Task<PagedResult<RepositoryStudentDto>> GetRepositoryStudentsAsync(Guid runId, PageQuery query, CancellationToken ct = default)
    {
        var _ = await uow.AnalysisRuns.GetByIdAsync(runId, ct) ?? throw new NotFoundException("repo", runId);
        var page = await uow.Users.SearchAsync(UserRole.Student, runId, null, query.Page, query.PageSize, ct);
        return page.Map(u => new RepositoryStudentDto(u.Id, u.StudentCode, u.Email, u.AnalysisRunId, u.AnalysisRun?.RepoName));
    }

    public async Task<PagedResult<RepositoryStudentDto>> GetAvailableStudentCandidatesAsync(Guid runId, RepositoryStudentQuery query, CancellationToken ct = default)
    {
        var _ = await uow.AnalysisRuns.GetByIdAsync(runId, ct) ?? throw new NotFoundException("repo", runId);
        var page = await uow.Users.SearchAsync(UserRole.Student, null, query.Search, query.Page, query.PageSize, ct);
        return page.Map(u => new RepositoryStudentDto(u.Id, u.StudentCode, u.Email, u.AnalysisRunId, u.AnalysisRun?.RepoName));
    }

    public async Task<AssignRepositoryStudentsResult> AssignStudentsAsync(Guid runId, AssignRepositoryStudentsRequest request, CancellationToken ct = default)
    {
        var run = await uow.AnalysisRuns.GetByIdAsync(runId, ct) ?? throw new NotFoundException("repo", runId);

        var codes = (request.StudentCodes ?? [])
            .Select(c => c.Trim().ToUpperInvariant())
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .Take(100)
            .ToList();

        if (codes.Count == 0)
            return new AssignRepositoryStudentsResult(0, 0, 0, 0);

        var created = 0;
        var moved = 0;
        var alreadyAssigned = 0;

        foreach (var code in codes)
        {
            var existing = await uow.Users.GetByStudentCodeAsync(code, ct);
            if (existing is not null)
            {
                if (existing.AnalysisRunId == runId)
                {
                    alreadyAssigned++;
                }
                else
                {
                    var tracked = await uow.Users.GetWithRepoAsync(existing.Id, asNoTracking: false, ct: ct);
                    if (tracked is not null)
                    {
                        tracked.AnalysisRunId = runId;
                        moved++;
                    }
                }
            }
            else
            {
                var email = $"{code.ToLowerInvariant()}@gmail.com";
                if (await uow.Users.EmailExistsAsync(email, null, ct))
                {
                    email = $"{code.ToLowerInvariant()}_{Guid.NewGuid().ToString("N")[..6]}@gmail.com";
                }

                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    StudentCode = code,
                    Role = UserRole.Student,
                    AnalysisRunId = runId,
                    CreatedAt = DateTime.UtcNow
                };
                await uow.Users.AddAsync(newUser, ct);
                created++;
            }
        }

        await uow.SaveChangesAsync(ct);
        return new AssignRepositoryStudentsResult(created + moved + alreadyAssigned, created, moved, alreadyAssigned);
    }

    public async Task RemoveStudentAsync(Guid runId, Guid userId, CancellationToken ct = default)
    {
        var user = await uow.Users.GetWithRepoAsync(userId, asNoTracking: false, ct: ct)
                   ?? throw new NotFoundException("Sinh viên không tồn tại.");

        if (user.AnalysisRunId == runId)
        {
            user.AnalysisRunId = null;
            await uow.SaveChangesAsync(ct);
        }
    }
}
