using PACE.Application.Analysis;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
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
    private async Task EnsureCanViewRepoAsync(Guid id, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) throw new UnauthorizedException("Bạn chưa đăng nhập.");
        if (currentUser.Role == UserRole.Admin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturerOfRepo = await uow.AnalysisRuns.IsLecturerOfRepoClassAsync(id, currentUser.UserId, ct);
            if (!isLecturerOfRepo)
                throw new ForbiddenException("Bạn không có quyền xem repository này vì không phụ trách lớp chứa repository.");
            return;
        }

        if (currentUser.Role == UserRole.Student)
        {
            var isStudentOfRepo = await uow.AnalysisRuns.IsStudentOfRepoAsync(id, currentUser.UserId, ct);
            if (!isStudentOfRepo)
                throw new ForbiddenException("Sinh viên chỉ được quyền xem repository của nhóm mình.");
            return;
        }

        throw new ForbiddenException();
    }

    private async Task EnsureCanManageRepoAsync(Guid id, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) throw new UnauthorizedException("Bạn chưa đăng nhập.");
        if (currentUser.Role == UserRole.Admin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturerOfRepo = await uow.AnalysisRuns.IsLecturerOfRepoClassAsync(id, currentUser.UserId, ct);
            if (!isLecturerOfRepo)
                throw new ForbiddenException("Giảng viên chỉ có quyền quản lý các repository thuộc lớp mình phụ trách.");
            return;
        }

        throw new ForbiddenException("Bạn không có quyền thực hiện thao tác này.");
    }

    public async Task<PagedResult<RepositoryStudentDto>> GetStudentsAsync(Guid id, bool available, PageQuery query, CancellationToken ct = default)
    {
        await EnsureCanManageRepoAsync(id, ct);
        _ = await uow.AnalysisRuns.GetByIdAsync(id, ct) ?? throw new NotFoundException("repo", id);
        var page = await uow.Users.SearchAsync(UserRole.Student, available ? null : id, query.Search, query.Page, query.PageSize, ct);
        return page.Map(u => new RepositoryStudentDto(u.Id, u.StudentCode, u.Email, u.AnalysisRunId, u.AnalysisRun?.RepoName));
    }

    public Task<AssignRepositoryStudentsResult> AssignStudentsAsync(Guid id, AssignRepositoryStudentsRequest request, CancellationToken ct = default)
    {
        return uow.ExecuteInTransactionAsync(async token =>
        {
            await EnsureCanManageRepoAsync(id, token);
            var codes = (request.StudentCodes ?? []).Select(c => c?.Trim().ToUpperInvariant() ?? "").Distinct().Order().ToList();
            if (codes.Count == 0 || codes.Count > 100 || codes.Any(c => !System.Text.RegularExpressions.Regex.IsMatch(c, "^[A-Z0-9][A-Z0-9_-]{1,19}$")))
                throw new BadRequestException("Nhập từ 1 đến 100 MSSV hợp lệ (2–20 ký tự chữ, số, _ hoặc -).");

            var run = await uow.AnalysisRuns.GetByIdAsync(id, token) ?? throw new NotFoundException("repo", id);
            int created = 0, moved = 0, already = 0;
            // Same lock namespace as quick-create; sorted order avoids bulk-request deadlocks.
            foreach (var code in codes)
            {
                await uow.StudentSubmissions.LockQuickCreateKeyAsync("student:" + code, token);
                var matches = await uow.Users.FindAsync(u => u.StudentCode != null && u.StudentCode.Trim().ToUpper() == code, ct: token);
                if (matches.Count > 1) throw new ConflictException($"MSSV {code} bị trùng trong DB.");
                var student = matches.SingleOrDefault();
                if (student != null)
                {
                    await uow.StudentSubmissions.LockUserAsync(student.Id, token);
                    student = await uow.Users.GetWithRepoAsync(student.Id, asNoTracking: false, ct: token)
                        ?? throw new ConflictException("Tài khoản vừa thay đổi, vui lòng thử lại.");
                    if (student.Role != UserRole.Student) throw new ConflictException($"MSSV {code} thuộc tài khoản admin/giảng viên.");
                    if (student.AnalysisRunId == id) { already++; continue; }
                    student.AnalysisRunId = id;
                    student.AnalysisRun = run;
                    moved++;
                }
                else
                {
                    var email = code.ToLowerInvariant() + "@gmail.com";
                    if (await uow.Users.EmailExistsAsync(email, null, token))
                        throw new ConflictException($"Email mặc định của {code} đã được tài khoản khác sử dụng.");
                    await uow.Users.AddAsync(new User { Id = Guid.NewGuid(), StudentCode = code, Email = email,
                        Role = UserRole.Student, AnalysisRunId = id }, token);
                    created++;
                }
            }
            return new AssignRepositoryStudentsResult(codes.Count, created, moved, already);
        }, ct);
    }

    public Task RemoveStudentAsync(Guid id, Guid studentId, CancellationToken ct = default)
    {
        return uow.ExecuteInTransactionAsync(async token =>
        {
            await EnsureCanManageRepoAsync(id, token);
            _ = await uow.AnalysisRuns.GetByIdAsync(id, token) ?? throw new NotFoundException("repo", id);
            await uow.StudentSubmissions.LockUserAsync(studentId, token);
            var student = await uow.Users.GetWithRepoAsync(studentId, asNoTracking: false, ct: token)
                ?? throw new NotFoundException("sinh viên", studentId);
            if (student.Role != UserRole.Student) throw new BadRequestException("Tài khoản này không phải sinh viên.");
            if (student.AnalysisRunId == null) return;
            if (student.AnalysisRunId != id) throw new ConflictException("Sinh viên đã chuyển sang repo khác. Vui lòng làm mới danh sách.");
            student.AnalysisRunId = null;
            student.AnalysisRun = null;
        }, ct);
    }

    public async Task<PagedResult<AnalysisRunDto>> GetPagedAsync(AnalysisRunQuery query, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated) throw new UnauthorizedException("Bạn chưa đăng nhập.");

        PagedResult<AnalysisRunSummary> page;

        if (currentUser.Role == UserRole.Admin)
        {
            page = await uow.AnalysisRuns.SearchSummariesAsync(
                query.Search, query.Page, query.PageSize, classId: query.ClassId, ct: ct);
        }
        else if (currentUser.Role == UserRole.Lecturer)
        {
            if (query.ClassId.HasValue)
            {
                var isLecturerOfClass = await uow.AnalysisRuns.IsLecturerOfClassAsync(query.ClassId.Value, currentUser.UserId, ct);
                if (!isLecturerOfClass)
                    throw new ForbiddenException("Bạn không phụ trách giảng dạy lớp học này.");

                page = await uow.AnalysisRuns.SearchSummariesAsync(
                    query.Search, query.Page, query.PageSize, classId: query.ClassId.Value, ct: ct);
            }
            else
            {
                var lecturerClassIds = await uow.AnalysisRuns.GetLecturerClassIdsAsync(currentUser.UserId, ct);
                page = await uow.AnalysisRuns.SearchSummariesAsync(
                    query.Search, query.Page, query.PageSize, allowedClassIds: lecturerClassIds, ct: ct);
            }
        }
        else if (currentUser.Role == UserRole.Student)
        {
            var studentRepoId = await uow.AnalysisRuns.GetStudentRepoIdAsync(currentUser.UserId, ct);
            if (!studentRepoId.HasValue)
            {
                return new PagedResult<AnalysisRunDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
            }

            page = await uow.AnalysisRuns.SearchSummariesAsync(
                query.Search, query.Page, query.PageSize, singleRepoId: studentRepoId.Value, ct: ct);
        }
        else
        {
            throw new ForbiddenException();
        }

        return page.Map(r => r.ToDto(AnalysisRunLock.IsRunning(r.Id)));
    }

    public async Task<AnalysisRunDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureCanViewRepoAsync(id, ct);
        var summary = await uow.AnalysisRuns.GetSummaryAsync(id, ct) ?? throw new NotFoundException("repo", id);
        var businesses = await businessService.GetByRunAsync(id, ct);
        var emails = await uow.Users.GetEmailsByRepoAsync(id, ct);
        return new AnalysisRunDetailDto(summary.ToDto(AnalysisRunLock.IsRunning(id)), businesses, emails);
    }

    public async Task<AnalysisRunDetailDto> GetMineAsync(CancellationToken ct = default)
    {
        var user = await uow.Users.GetWithRepoAsync(currentUser.UserId, ct: ct);
        if (user?.AnalysisRunId is not { } runId)
            throw new NotFoundException("Tài khoản của bạn chưa được gán repo nào.");
        return await GetDetailAsync(runId, ct);
    }

    public async Task<AnalysisRunDto> UpdateAsync(Guid id, UpdateAnalysisRunRequest request, CancellationToken ct = default)
    {
        await EnsureCanManageRepoAsync(id, ct);
        if (RepoUrlHelper.FindProblem(request.RepoUrl) is { } problem) throw new BadRequestException(problem);
        if (!AnalysisRunLock.TryAcquire(id)) throw new ConflictException("Repo đang được phân tích, vui lòng đợi xong rồi sửa link.");
        try
        {
            var run = await uow.AnalysisRuns.GetByIdAsync(id, ct) ?? throw new NotFoundException("repo", id);
            if (run.IsArchived) throw new BadRequestException("Không thể sửa bản lịch sử.");
            var url = RepoUrlHelper.Normalize(request.RepoUrl);
            await EnsureUrlAvailableAsync(url, id, ct);
            run.AnalyzedRepoUrl ??= run.RepoUrl;
            run.RepoUrl = url;
            run.RepoName = string.IsNullOrWhiteSpace(request.RepoName) ? RepoUrlHelper.DeriveName(url) : request.RepoName.Trim();
            await uow.SaveChangesAsync(ct);
            return (await GetDetailAsync(id, ct)).Run;
        }
        finally { AnalysisRunLock.Release(id); }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureCanManageRepoAsync(id, ct);
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
        await EnsureCanManageRepoAsync(id, ct);
        var run = await uow.AnalysisRuns.GetByIdAsync(id, ct) ?? throw new NotFoundException("repo", id);
        var businesses = StoredBusinessTemplate.Load(run)
                         ?? throw new NotFoundException("Repo chưa có template nghiệp vụ nào được lưu.");
        return ToTemplateDto(run, businesses);
    }

    public async Task<AnalysisRunTemplateDto> SaveTemplateAsync(Guid id, BusinessTemplateRequest request, CancellationToken ct = default)
    {
        await EnsureCanManageRepoAsync(id, ct);
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
        await EnsureCanManageRepoAsync(id, ct);
        var run = await uow.AnalysisRuns.GetByIdAsync(id, ct) ?? throw new NotFoundException("repo", id);
        var allNames = await uow.AnalysisRuns.GetAllRepoNamesAsync(ct);
        var baseName = run.RepoName ?? (run.RepoUrl != null ? RepoUrlHelper.DeriveName(run.RepoUrl) : "Repository");
        return RepoUrlHelper.ComputeCopyName(baseName, allNames);
    }
}
