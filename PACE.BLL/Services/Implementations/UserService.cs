using System.ComponentModel.DataAnnotations;
using ClosedXML.Excel;
using PACE.BLL.Common;
using PACE.BLL.Dtos;
using PACE.BLL.Exceptions;
using PACE.BLL.Mappings;
using PACE.BLL.Services.Interfaces;
using PACE.DAL.Models;
using PACE.DAL.Repositories;
using PACE.DAL.Repositories.Interfaces;

namespace PACE.BLL.Services.Implementations;

public class UserService(IUnitOfWork uow, ICurrentUserService currentUser) : IUserService
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
        var rows = ReadImportRows(excelStream);
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
            if (string.IsNullOrWhiteSpace(row.RepoUrl) || !RepoUrlHelper.IsSupportedUrl(row.RepoUrl))
            {
                skipped.Add(new ImportRowError(row.Row, row.Email, row.RepoUrl, "Link repo không hợp lệ."));
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

    private record ImportRow(int Row, string? Email, string? RepoUrl, string? RepoName);

    private static List<ImportRow> ReadImportRows(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new BadRequestException("File Excel không đọc được (cần định dạng .xlsx).", ex);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new BadRequestException("File Excel không có sheet nào.");
            var header = sheet.FirstRowUsed() ?? throw new BadRequestException("File Excel trống.");

            int? emailCol = null, repoCol = null, nameCol = null;
            foreach (var cell in header.CellsUsed())
            {
                var title = cell.GetString().Trim().ToLowerInvariant();
                if (emailCol is null && (title.Contains("email") || title.Contains("mail"))) emailCol = cell.Address.ColumnNumber;
                else if (repoCol is null && (title.Contains("repo") || title.Contains("github") || title.Contains("link") || title.Contains("url"))) repoCol = cell.Address.ColumnNumber;
                else if (nameCol is null && (title.Contains("nhóm") || title.Contains("group") || title.Contains("name") || title.Contains("tên"))) nameCol = cell.Address.ColumnNumber;
            }

            if (emailCol is null || repoCol is null)
                throw new BadRequestException("Dòng tiêu đề phải có cột Email và cột Repo (link GitHub).");

            var result = new List<ImportRow>();
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
            {
                var email = row.Cell(emailCol.Value).GetString().Trim();
                var repo = row.Cell(repoCol.Value).GetString().Trim();
                if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(repo)) continue;
                var name = nameCol.HasValue ? row.Cell(nameCol.Value).GetString().Trim() : null;
                result.Add(new ImportRow(row.RowNumber(), email, repo, name));
            }

            return result;
        }
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
        if (role == UserRole.Admin) return null;
        if (runId is null) throw new BadRequestException("Sinh viên bắt buộc thuộc một repo (AnalysisRunId).");
        if (!await uow.AnalysisRuns.AnyAsync(r => r.Id == runId, ct)) throw new NotFoundException("repo", runId);
        return runId;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
