using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record SaveGitAccountRequest([MaxLength(200)] string? GitHub = null, [MaxLength(5)] List<string>? Emails = null);
public record GitAccountView(Guid StudentId, string StudentCode, string FullName, string? GitHub, IReadOnlyList<string> Emails, DateTime? UpdatedAt, bool IsMe);

/// <summary>Kiểm và chuẩn hóa thông tin tài khoản git được nhập vào.</summary>
public static partial class GitAccountRules
{
    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9]|-(?=[a-z0-9])){0,38}$")]
    private static partial Regex Username();

    /// <summary>Nhận tên, "@tên" hoặc đường dẫn hồ sơ GitHub; trả tên chữ thường, hoặc null nếu bỏ trống.</summary>
    public static string? Normalize(string? input)
    {
        var text = input?.Trim() ?? "";
        if (text.Length == 0) return null;
        text = Regex.Replace(text, @"^(https?://)?(www\.)?github\.com/", "", RegexOptions.IgnoreCase).TrimStart('@').TrimEnd('/');
        text = text.ToLowerInvariant();
        return Username().IsMatch(text) ? text : throw new BadRequestException("Tên tài khoản GitHub không hợp lệ. Chỉ điền tên tài khoản hoặc đường dẫn hồ sơ.");
    }

    public static List<string> Emails(IEnumerable<string>? input)
    {
        var emails = (input ?? []).Select(e => e?.Trim().ToLowerInvariant() ?? "").Where(e => e.Length > 0).Distinct().ToList();
        if (emails.Count > 5 || emails.Any(e => e.Length > 255 || e.Contains(',') || !MailAddress.TryCreate(e, out var parsed) || parsed.Address != e))
            throw new BadRequestException("Điền tối đa 5 email commit hợp lệ.");
        return emails;
    }

    public static List<string> Split(string? stored) => stored?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList() ?? [];
}

/// <summary>Tài khoản git của thành viên một nhóm. Sinh viên khai của mình; giảng viên lớp và admin điền giúp được.</summary>
[ApiController, Route("api/groups/{groupId:guid}/git-accounts"), Authorize]
public class GroupGitAccountsController(AppDbContext db, ICurrentUserService current, IGitHubDirectory github) : ControllerBase
{
    private async Task<(List<User> Members, bool Staff)> Load(Guid groupId, CancellationToken ct)
    {
        var group = await db.Groups.AsNoTracking().Where(g => g.Id == groupId)
            .Select(g => new { g.Class.LecturerId, Codes = g.Members.Select(m => m.StudentCode).ToList() })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Group unavailable.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == current.UserId, ct)
            ?? throw new UnauthorizedException("Account unavailable.");
        var staff = user.Role == UserRole.Admin || (user.Role == UserRole.Lecturer && group.LecturerId == user.Id);
        var member = user.Role == UserRole.Student && user.StudentCode != null && group.Codes.Contains(user.StudentCode);
        if (!staff && !member) throw new ForbiddenException();
        var members = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Student && u.StudentCode != null && group.Codes.Contains(u.StudentCode))
            .OrderBy(u => u.StudentCode).ToListAsync(ct);
        return (members, staff);
    }

    private async Task<List<GitAccountView>> Views(List<User> members, CancellationToken ct)
    {
        var ids = members.Select(m => m.Id).ToList();
        var accounts = await db.StudentGitAccounts.AsNoTracking().Where(a => ids.Contains(a.UserId)).ToDictionaryAsync(a => a.UserId, ct);
        return members.Select(m =>
        {
            var account = accounts.GetValueOrDefault(m.Id);
            return new GitAccountView(m.Id, m.StudentCode!, string.IsNullOrWhiteSpace(m.FullName) ? m.Email : m.FullName,
                account?.GitHubUsername, GitAccountRules.Split(account?.Emails), account?.UpdatedAt, m.Id == current.UserId);
        }).ToList();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GitAccountView>>> List(Guid groupId, CancellationToken ct)
        => Ok(await Views((await Load(groupId, ct)).Members, ct));

    [HttpPut("{studentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<GitAccountView>>> Save(Guid groupId, Guid studentId, SaveGitAccountRequest request, CancellationToken ct)
    {
        var (members, staff) = await Load(groupId, ct);
        if (members.All(m => m.Id != studentId)) throw new NotFoundException("Student is not in this group.");
        if (!staff && studentId != current.UserId) throw new ForbiddenException();

        var name = GitAccountRules.Normalize(request.GitHub);
        var emails = GitAccountRules.Emails(request.Emails);
        var account = await db.StudentGitAccounts.SingleOrDefaultAsync(a => a.UserId == studentId, ct);

        if (name != null && name != account?.GitHubUsername)
        {
            if (await db.StudentGitAccounts.AnyAsync(a => a.GitHubUsername == name && a.UserId != studentId, ct))
                throw new ConflictException("Tài khoản GitHub này đã được một sinh viên khác khai. Hãy kiểm lại hoặc báo giảng viên.");
            if (await github.UserExistsAsync(name, ct) == false)
                throw new BadRequestException($"GitHub không có tài khoản \"{name}\". Kiểm lại chính tả.");
        }

        if (name == null && emails.Count == 0)
        {
            if (account != null) db.StudentGitAccounts.Remove(account);
        }
        else
        {
            if (account == null) db.StudentGitAccounts.Add(account = new StudentGitAccount { UserId = studentId });
            account.GitHubUsername = name;
            account.Emails = emails.Count == 0 ? null : string.Join(',', emails);
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = current.UserId;
        }
        db.AuditEvents.Add(new AuditEvent { ActorId = current.UserId, ActorName = current.Email, ResourceId = studentId, EventType = "GIT_ACCOUNT_SAVED", Details = name ?? "" });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("Tài khoản GitHub vừa được người khác khai. Hãy tải lại."); }
        return Ok(await Views(members, ct));
    }
}
