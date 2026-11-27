using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Exceptions;
using PACE.Application.Services.Implementations;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record GroupSetupRequest(
    [Range(1, 10)] int MinMembers,
    [Range(1, 10)] int MaxMembers,
    [Required] DateTimeOffset? Deadline);

public record GroupPlacement([Required] string StudentCode, Guid GroupId);

public record ApplyGroupPlanRequest([Required, MaxLength(500)] List<GroupPlacement> Placements);

public record BoardStudent(string StudentCode, string? FullName, string? Email);

public record BoardMember(string StudentCode, string? FullName, string? Email, string Role, DateTime JoinedAt);

public record BoardGroup(Guid Id, string Name, string? TopicName, bool HasRepository, IReadOnlyList<BoardMember> Members);

public record GroupRegistrationState(DateTime Deadline, bool IsLocked, bool IsOpen);

public record GroupBoard(
    Guid ClassId, int MinMembers, int MaxMembers, int StudentCount, GroupRegistrationState? Registration,
    IReadOnlyList<BoardGroup> Groups, IReadOnlyList<BoardStudent> Unassigned, string? MyStudentCode);

public record PlannedMember(string StudentCode, string? FullName, Guid? FromGroupId);

public record PlannedGroup(Guid Id, string Name, IReadOnlyList<PlannedMember> Members);

public record GroupPlanView(int MinMembers, int MaxMembers, IReadOnlyList<PlannedGroup> Groups, IReadOnlyList<BoardStudent> Unplaced);

/// <summary>
/// Chia nhóm: giảng viên mở các nhóm trống kèm hạn đăng ký; sinh viên tự vào nhóm (người vào đầu tiên là nhóm trưởng);
/// hết hạn, giảng viên xem đề xuất xếp những người còn lại rồi chốt.
/// </summary>
[ApiController, Authorize, Route("api")]
public class GroupFormationController(AppDbContext db, TeachingAccess access) : ControllerBase
{
    [HttpGet("classes/{classId:guid}/group-board")]
    public async Task<ActionResult<GroupBoard>> Board(Guid classId, CancellationToken ct)
    {
        var user = await access.User(ct);
        var cls = await FindClass(classId, ct);
        var students = await ActiveStudents(classId, ct);
        var isStaff = IsClassStaff(user, cls);
        if (!isStaff && !(user.Role == UserRole.Student && students.Any(s => s.StudentCode == user.StudentCode)))
            throw new ForbiddenException("Bạn không thuộc lớp này.");

        var groups = await db.Groups.AsNoTracking().Where(g => g.ClassId == classId)
            .Select(g => new { g.Id, g.Name, g.TopicName, HasRepository = g.AnalysisRuns.Any() })
            .ToListAsync(ct);
        var members = await db.GroupMembers.AsNoTracking().Where(m => m.Group.ClassId == classId)
            .Select(m => new { m.GroupId, m.StudentCode, m.Role, m.JoinedAt, m.StudentProfile.User.FullName, m.StudentProfile.User.Email })
            .ToListAsync(ct);
        var registration = await Registration(classId, ct);
        var placed = members.Select(m => m.StudentCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Ok(new GroupBoard(
            classId, cls.MinGroupMembers, cls.MaxGroupMembers, students.Count,
            registration is null ? null : new GroupRegistrationState(registration.Deadline, registration.IsLocked, IsOpen(registration)),
            groups.OrderBy(g => g.Name.Length).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new BoardGroup(g.Id, g.Name, g.TopicName, g.HasRepository,
                    members.Where(m => m.GroupId == g.Id).OrderByDescending(m => IsLeader(m.Role)).ThenBy(m => m.JoinedAt)
                        .Select(m => new BoardMember(m.StudentCode, m.FullName, m.Email, m.Role, m.JoinedAt)).ToList()))
                .ToList(),
            students.Where(s => !placed.Contains(s.StudentCode)).ToList(),
            user.Role == UserRole.Student ? user.StudentCode : null));
    }

    /// <summary>Mở đăng ký nhóm: lưu số thành viên, tạo đủ nhóm trống và đặt hạn.</summary>
    [HttpPost("classes/{classId:guid}/group-setup"), Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Setup(Guid classId, GroupSetupRequest request, CancellationToken ct)
    {
        await RequireClassStaff(classId, ct);
        if (request.MaxMembers < request.MinMembers)
            throw new BadRequestException("Số thành viên tối đa phải lớn hơn hoặc bằng tối thiểu.");
        var deadline = request.Deadline!.Value.UtcDateTime;
        if (deadline <= DateTime.UtcNow)
            throw new BadRequestException("Hạn đăng ký nhóm phải ở tương lai.");

        await Serializable(async () =>
        {
            var cls = await FindClass(classId, ct);
            var students = (await ActiveStudents(classId, ct)).Count;
            if (students == 0)
                throw new BadRequestException("Lớp chưa có sinh viên.");

            var groups = await db.Groups.Include(g => g.Members).Include(g => g.AnalysisRuns).Include(g => g.ChangeRequests)
                .Where(g => g.ClassId == classId).ToListAsync(ct);
            if (groups.Any(g => g.Members.Count > request.MaxMembers))
                throw new BadRequestException("Đang có nhóm đông hơn số thành viên tối đa mới.");

            var target = Math.Max(GroupPlanner.GroupCount(students, request.MinMembers), groups.Count(g => g.Members.Count > 0));
            if (target * request.MaxMembers < students)
                throw new BadRequestException($"{students} sinh viên không chia được vào {target} nhóm tối đa {request.MaxMembers} người. Hãy giảm số tối thiểu hoặc tăng số tối đa.");

            // Bỏ bớt nhóm trống chưa gắn dữ liệu nào khi cần ít nhóm hơn.
            var submitted = await db.GroupTopicSubmissions.Where(s => s.Group.ClassId == classId).Select(s => s.GroupId).Distinct().ToListAsync(ct);
            var spare = groups.Where(g => IsDisposable(g, submitted)).OrderByDescending(g => g.CreatedAt).Take(Math.Max(0, groups.Count - target)).ToList();
            db.Groups.RemoveRange(spare);

            var names = groups.Except(spare).Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var number = 1; names.Count < target; number++)
            {
                var name = $"Nhóm {number}";
                if (!names.Add(name)) continue;
                db.Groups.Add(new Group { ClassId = classId, Name = name, CreatedAt = DateTime.UtcNow, IsApproved = true });
            }

            cls.MinGroupMembers = request.MinMembers;
            cls.MaxGroupMembers = request.MaxMembers;
            await SetRegistration(classId, deadline, false, ct);
        }, ct);

        return NoContent();
    }

    /// <summary>Sinh viên vào một nhóm còn chỗ trong thời gian đăng ký. Người đầu tiên của nhóm là nhóm trưởng.</summary>
    [HttpPost("groups/{groupId:guid}/join"), Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> Join(Guid groupId, CancellationToken ct)
    {
        await Serializable(async () =>
        {
            var (user, group) = await RequireOpenRegistration(groupId, ct);
            if (await db.GroupMembers.AnyAsync(m => m.Group.ClassId == group.ClassId && m.StudentCode == user.StudentCode, ct))
                throw new ConflictException("Bạn đã ở trong một nhóm của lớp này.");
            var size = await db.GroupMembers.CountAsync(m => m.GroupId == groupId, ct);
            if (size >= group.Class.MaxGroupMembers)
                throw new ConflictException("Nhóm đã đủ người.");

            db.GroupMembers.Add(new GroupMember { GroupId = groupId, StudentCode = user.StudentCode!, Role = size == 0 ? "Leader" : "Member", JoinedAt = DateTime.UtcNow });
            await CancelInvitations(group.ClassId, [user.StudentCode!], ct);
        }, ct);

        return NoContent();
    }

    /// <summary>Rời nhóm trong thời gian đăng ký; nhóm trưởng rời thì người vào sớm nhất lên thay.</summary>
    [HttpPost("groups/{groupId:guid}/leave"), Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> Leave(Guid groupId, CancellationToken ct)
    {
        await Serializable(async () =>
        {
            var (user, group) = await RequireOpenRegistration(groupId, ct);
            var members = await db.GroupMembers.Where(m => m.GroupId == groupId).OrderBy(m => m.JoinedAt).ToListAsync(ct);
            var mine = members.SingleOrDefault(m => m.StudentCode == user.StudentCode)
                ?? throw new NotFoundException("Bạn không ở trong nhóm này.");

            db.GroupMembers.Remove(mine);
            var rest = members.Where(m => m != mine).ToList();
            if (IsLeader(mine.Role) && rest.Count > 0) rest[0].Role = "Leader";
            if (rest.Count == 0)
            {
                var pending = await db.GroupInvitations.Where(i => i.GroupId == group.Id && i.Status == GroupInvitationStatus.Pending).ToListAsync(ct);
                foreach (var invitation in pending) Cancel(invitation);
            }
        }, ct);

        return NoContent();
    }

    /// <summary>Đề xuất xếp những sinh viên chưa có nhóm và các nhóm thiếu người. Không thay đổi dữ liệu.</summary>
    [HttpGet("classes/{classId:guid}/group-plan"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<GroupPlanView>> Plan(Guid classId, CancellationToken ct)
    {
        var cls = await RequireClassStaff(classId, ct);
        var students = await ActiveStudents(classId, ct);
        var groups = await db.Groups.AsNoTracking().Include(g => g.Members).Where(g => g.ClassId == classId).ToListAsync(ct);
        // Một người lỡ nằm ở hai nhóm thì lấy nhóm vào trước, không để cả đề xuất bị lỗi.
        var current = groups.SelectMany(g => g.Members).GroupBy(m => m.StudentCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.JoinedAt).First().GroupId, StringComparer.OrdinalIgnoreCase);
        var byCode = students.ToDictionary(s => s.StudentCode, StringComparer.OrdinalIgnoreCase);

        var plan = GroupPlanner.Plan(
            groups.Select(g => new PlanInputGroup(g.Id, g.Name, g.Members.OrderByDescending(m => IsLeader(m.Role)).ThenBy(m => m.JoinedAt).Select(m => m.StudentCode).ToList())).ToList(),
            students.Where(s => !current.ContainsKey(s.StudentCode)).Select(s => s.StudentCode).ToList(),
            cls.MinGroupMembers, cls.MaxGroupMembers);

        return Ok(new GroupPlanView(
            cls.MinGroupMembers, cls.MaxGroupMembers,
            plan.Groups.Select(g => new PlannedGroup(g.Id, g.Name, g.Members
                .Select(code => new PlannedMember(code, byCode.GetValueOrDefault(code)?.FullName, current.TryGetValue(code, out var from) ? from : null))
                .ToList())).ToList(),
            plan.Unplaced.Select(code => byCode[code]).ToList()));
    }

    /// <summary>Áp dụng cách xếp giảng viên đã duyệt: chuyển người, bỏ nhóm trống, cử nhóm trưởng còn thiếu và chốt đăng ký.</summary>
    [HttpPost("classes/{classId:guid}/group-plan"), Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Apply(Guid classId, ApplyGroupPlanRequest request, CancellationToken ct)
    {
        await RequireClassStaff(classId, ct);
        var placements = request.Placements
            .Select(p => (Code: p.StudentCode.Trim().ToUpperInvariant(), p.GroupId)).ToList();
        if (placements.Select(p => p.Code).Distinct().Count() != placements.Count)
            throw new BadRequestException("Mỗi sinh viên chỉ được xếp vào một nhóm.");

        await Serializable(async () =>
        {
            var cls = await FindClass(classId, ct);
            var active = (await ActiveStudents(classId, ct)).Select(s => s.StudentCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var groups = await db.Groups.Include(g => g.Members).Include(g => g.AnalysisRuns).Include(g => g.ChangeRequests)
                .Where(g => g.ClassId == classId).ToListAsync(ct);

            foreach (var (code, groupId) in placements)
            {
                if (!active.Contains(code)) throw new BadRequestException($"Sinh viên {code} không học lớp này.");
                var target = groups.SingleOrDefault(g => g.Id == groupId) ?? throw new BadRequestException("Nhóm được chọn không thuộc lớp này.");
                var existing = groups.SelectMany(g => g.Members).SingleOrDefault(m => m.StudentCode.Equals(code, StringComparison.OrdinalIgnoreCase));
                if (existing?.GroupId == groupId) continue;
                if (existing is not null)
                {
                    groups.Single(g => g.Id == existing.GroupId).Members.Remove(existing);
                    db.GroupMembers.Remove(existing);
                }
                var member = new GroupMember { GroupId = groupId, StudentCode = code, Role = "Member", JoinedAt = DateTime.UtcNow };
                target.Members.Add(member);
                db.GroupMembers.Add(member);
            }

            var over = groups.FirstOrDefault(g => g.Members.Count > cls.MaxGroupMembers);
            if (over is not null)
                throw new BadRequestException($"{over.Name} vượt quá {cls.MaxGroupMembers} người.");

            foreach (var group in groups.Where(g => g.Members.Count > 0 && !g.Members.Any(m => IsLeader(m.Role))))
                group.Members.OrderBy(m => m.JoinedAt).ThenBy(m => m.StudentCode).First().Role = "Leader";

            var submitted = await db.GroupTopicSubmissions.Where(s => s.Group.ClassId == classId).Select(s => s.GroupId).Distinct().ToListAsync(ct);
            db.Groups.RemoveRange(groups.Where(g => IsDisposable(g, submitted)));

            await CancelInvitations(classId, placements.Select(p => p.Code).ToList(), ct);
            var registration = await Registration(classId, ct);
            await SetRegistration(classId, registration?.Deadline ?? DateTime.UtcNow, true, ct);
        }, ct);

        return NoContent();
    }

    private static bool IsLeader(string role) => role.Equals("Leader", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpen(Milestone registration) => !registration.IsLocked && DateTime.UtcNow <= registration.Deadline;

    private static bool IsDisposable(Group group, List<Guid> submitted) =>
        group.Members.Count == 0 && group.AnalysisRuns.Count == 0 && group.ChangeRequests.Count == 0 &&
        group.CourseTopicId is null && !submitted.Contains(group.Id);

    private static bool IsClassStaff(User user, Class cls) =>
        user.Role == UserRole.Admin || (user.Role == UserRole.Lecturer && cls.LecturerId == user.Id);

    private async Task<Class> FindClass(Guid classId, CancellationToken ct) =>
        await db.Classes.SingleOrDefaultAsync(c => c.Id == classId, ct) ?? throw new NotFoundException("Lớp học không tồn tại.");

    private async Task<Class> RequireClassStaff(Guid classId, CancellationToken ct)
    {
        var user = await access.User(ct);
        var cls = await FindClass(classId, ct);
        if (!IsClassStaff(user, cls)) throw new ForbiddenException("Chỉ giảng viên phụ trách lớp mới được chia nhóm.");
        return cls;
    }

    private async Task<(User User, Group Group)> RequireOpenRegistration(Guid groupId, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await db.Groups.Include(g => g.Class).SingleOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");
        if (user.StudentCode is null || !await db.StudentClasses.AnyAsync(s => s.ClassId == group.ClassId && s.StudentCode == user.StudentCode && s.Status == 1, ct))
            throw new ForbiddenException("Bạn không thuộc lớp này.");
        var registration = await Registration(group.ClassId, ct);
        if (registration is null || !IsOpen(registration))
            throw new BadRequestException("Lớp không trong thời gian đăng ký nhóm.");
        return (user, group);
    }

    private Task<List<BoardStudent>> ActiveStudents(Guid classId, CancellationToken ct) =>
        db.StudentClasses.AsNoTracking().Where(s => s.ClassId == classId && s.Status == 1)
            .OrderBy(s => s.StudentCode)
            .Select(s => new BoardStudent(s.StudentCode, s.StudentProfile.User.FullName, s.StudentProfile.User.Email))
            .ToListAsync(ct);

    private Task<Milestone?> Registration(Guid classId, CancellationToken ct) =>
        db.Milestones.Where(m => m.ClassId == classId && m.Type == MilestoneType.GroupRegistration)
            .OrderBy(m => m.CreatedAt).FirstOrDefaultAsync(ct);

    private async Task SetRegistration(Guid classId, DateTime deadline, bool locked, CancellationToken ct)
    {
        var registration = await Registration(classId, ct);
        if (registration is null)
        {
            db.Milestones.Add(new Milestone
            {
                ClassId = classId, Title = "Đăng ký nhóm", Description = "Hạn đăng ký và chốt danh sách nhóm",
                Type = MilestoneType.GroupRegistration, Deadline = deadline, IsLocked = locked, OrderIndex = 1, CreatedAt = DateTime.UtcNow
            });
            return;
        }

        registration.Deadline = deadline;
        registration.IsLocked = locked;
        registration.UpdatedAt = DateTime.UtcNow;
    }

    private async Task CancelInvitations(Guid classId, List<string> studentCodes, CancellationToken ct)
    {
        var pending = await db.GroupInvitations
            .Where(i => i.Group.ClassId == classId && i.Status == GroupInvitationStatus.Pending && studentCodes.Contains(i.InvitedStudentCode))
            .ToListAsync(ct);
        foreach (var invitation in pending) Cancel(invitation);
    }

    private static void Cancel(GroupInvitation invitation)
    {
        invitation.Status = GroupInvitationStatus.Cancelled;
        invitation.RespondedAt = DateTime.UtcNow;
    }

    // Hai sinh viên bấm vào cùng một nhóm không được cùng thành nhóm trưởng hay làm nhóm vượt sĩ số.
    private Task Serializable(Func<Task> action, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await action();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
}
