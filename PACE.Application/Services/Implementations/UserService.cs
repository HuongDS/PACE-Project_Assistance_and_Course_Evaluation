using System.ComponentModel.DataAnnotations;
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

public class UserService(IUnitOfWork uow, ICurrentUserService currentUser, IUserImportReader importReader) : IUserService
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public async Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query, CancellationToken ct = default)
    {
        var page = await uow.Users.SearchAsync(query.Role, query.AnalysisRunId, query.Search, query.Page, query.PageSize, ct);
        return page.Map(u => u.ToDto());
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        => (await LoadAsync(id, true, ct)).ToDto();

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        await EnsureEmailAvailableAsync(email, null, ct);
        var runId = await ValidateRoleAndRunAsync(request.Role, request.AnalysisRunId, ct);

        var user = new User { Id = Guid.NewGuid(), Email = email, Role = request.Role, AnalysisRunId = runId };
        await uow.Users.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await LoadAsync(id, false, ct);
        var email = NormalizeEmail(request.Email);
        await EnsureEmailAvailableAsync(email, id, ct);
        var runId = await ValidateRoleAndRunAsync(request.Role, request.AnalysisRunId, ct);

        if (user.Id == currentUser.UserId && request.Role != UserRole.Admin)
            throw new BadRequestException("Không thể tự hạ quyền Admin của chính mình.");

        user.Email = email;
        user.Role = request.Role;
        user.AnalysisRunId = runId;
        await uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (id == currentUser.UserId) throw new BadRequestException("Không thể tự xoá tài khoản đang đăng nhập.");
        var user = await LoadAsync(id, false, ct);
        uow.Users.Remove(user);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<ImportUsersResult> ImportFromExcelAsync(Stream excelStream, CancellationToken ct = default)
    {
        var rows = importReader.Read(excelStream);
        var skipped = new List<ImportRowError>();

        var existingEmails = await uow.Users.GetAllEmailsAsync(ct);
        var runs = await uow.AnalysisRuns.GetAllWithRepoUrlAsync(ct);
        var runsByUrl = new Dictionary<string, AnalysisRun>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in runs) runsByUrl.TryAdd(RepoUrlHelper.Normalize(run.RepoUrl!), run);

        var newUsers = new List<User>();
        var newRuns = new List<AnalysisRun>();
        var seenInFile = new HashSet<string>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Email) || !EmailValidator.IsValid(row.Email.Trim()))
            {
                skipped.Add(new ImportRowError(row.Row, row.Email, row.RepoUrl, "Email không hợp lệ."));
                continue;
            }
            if (RepoUrlHelper.FindProblem(row.RepoUrl) is { } repoProblem)
            {
                skipped.Add(new ImportRowError(row.Row, row.Email, row.RepoUrl, repoProblem));
                continue;
            }

            var email = NormalizeEmail(row.Email);
            if (existingEmails.Contains(email) || !seenInFile.Add(email))
            {
                skipped.Add(new ImportRowError(row.Row, email, row.RepoUrl, "Email đã tồn tại."));
                continue;
            }

            var url = RepoUrlHelper.Normalize(row.RepoUrl);
            if (!runsByUrl.TryGetValue(url, out var run))
            {
                run = new AnalysisRun
                {
                    Id = Guid.NewGuid(),
                    RepoUrl = url,
                    RepoName = string.IsNullOrWhiteSpace(row.RepoName) ? RepoUrlHelper.DeriveName(url) : row.RepoName.Trim()
                };
                runsByUrl[url] = run;
                newRuns.Add(run);
            }

            newUsers.Add(new User { Id = Guid.NewGuid(), Email = email, Role = UserRole.Student, AnalysisRunId = run.Id });
        }

        await uow.ExecuteInTransactionAsync(async token =>
        {
            await uow.AnalysisRuns.AddRangeAsync(newRuns, token);
            await uow.Users.AddRangeAsync(newUsers, token);
        }, ct);

        return new ImportUsersResult(rows.Count, newUsers.Count, newRuns.Count, skipped);
    }

    private async Task<User> LoadAsync(Guid id, bool asNoTracking, CancellationToken ct)
        => await uow.Users.GetWithRepoAsync(id, asNoTracking, ct)
           ?? throw new NotFoundException("người dùng", id);

    private async Task EnsureEmailAvailableAsync(string email, Guid? exceptId, CancellationToken ct)
    {
        if (await uow.Users.EmailExistsAsync(email, exceptId, ct))
            throw new ConflictException($"Email {email} đã tồn tại.");
    }

    private async Task<Guid?> ValidateRoleAndRunAsync(UserRole role, Guid? runId, CancellationToken ct)
    {
        if (!Enum.IsDefined(role)) throw new BadRequestException("Invalid role.");
        if (role is UserRole.Admin or UserRole.Lecturer) return null;
        if (runId is null) throw new BadRequestException("Sinh viên bắt buộc thuộc một repo (AnalysisRunId).");
        if (!await uow.AnalysisRuns.AnyAsync(r => r.Id == runId, ct)) throw new NotFoundException("repo", runId);
        return runId;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
