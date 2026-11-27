using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class GroupService(
    IUnitOfWork uow,
    ICurrentUserService currentUser) : IGroupService
{
    private async Task EnsureStaffOrMemberAccessAsync(Guid classId, Guid? groupId, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        if (currentUser.Role == UserRole.Student && groupId.HasValue)
        {
            var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            if (!string.IsNullOrEmpty(studentCode))
            {
                var isMember = await uow.Groups.GetMemberAsync(groupId.Value, studentCode, ct);
                if (isMember != null) return;
            }
        }

        throw new ForbiddenException("Bạn không có quyền thực hiện thao tác trên nhóm này.");
    }

    private async Task EnsureStaffAccessAsync(Guid classId, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        throw new ForbiddenException("Chỉ quản trị viên hoặc giảng viên phụ trách lớp mới có quyền thực hiện.");
    }

    private async Task EnsureLeaderOrStaffAccessAsync(Group group, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(group.ClassId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        if (currentUser.Role == UserRole.Student)
        {
            var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            if (!string.IsNullOrEmpty(studentCode))
            {
                var member = await uow.Groups.GetMemberAsync(group.Id, studentCode, ct);
                if (member != null && member.Role.Equals("Leader", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        throw new ForbiddenException("Chỉ nhóm trưởng hoặc giảng viên mới có quyền thực hiện thao tác này.");
    }

    public async Task<PagedResult<GroupDto>> GetPagedAsync(GroupQuery query, CancellationToken ct = default)
    {
        return await uow.Groups.SearchGroupsAsync(query, ct);
    }

    public async Task<GroupDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetGroupDetailAsync(id, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        return group;
    }

    public async Task<GroupDto> CreateAsync(CreateGroupRequest request, CancellationToken ct = default)
    {
        string? creatorStudentCode = null;

        if (currentUser.Role == UserRole.Student)
        {
            var classExists = await uow.Classes.ClassExistsAsync(request.ClassId, ct);
            if (!classExists)
                throw new NotFoundException("Lớp học không tồn tại.");

            // Kiểm tra mốc tiến độ đăng ký nhóm (Milestone: GroupRegistration)
            var groupMilestone = await uow.Milestones.GetClassMilestoneByTypeAsync(request.ClassId, MilestoneType.GroupRegistration, ct);
            if (groupMilestone != null)
            {
                if (groupMilestone.IsLocked)
                    throw new BadRequestException("Giai đoạn đăng ký nhóm cho lớp học này đã bị khóa.");

                if (DateTime.UtcNow > groupMilestone.Deadline)
                    throw new BadRequestException($"Đã hết thời hạn đăng ký nhóm cho lớp học này (Hạn: {groupMilestone.Deadline:dd/MM/yyyy HH:mm}).");
            }

            creatorStudentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            if (string.IsNullOrEmpty(creatorStudentCode))
                throw new ForbiddenException("Không tìm thấy thông tin sinh viên cho tài khoản hiện tại.");

            var isInClass = await uow.Classes.IsStudentOfClassAsync(request.ClassId, currentUser.UserId, creatorStudentCode, ct);
            if (!isInClass)
                throw new ForbiddenException("Bạn không thuộc danh sách sinh viên của lớp học này.");

            var alreadyInGroup = await uow.Groups.IsStudentInAnyGroupInClassAsync(request.ClassId, creatorStudentCode, ct);
            if (alreadyInGroup)
                throw new ConflictException("Bạn đã là thành viên của một nhóm khác trong lớp học này.");
        }
        else
        {
            await EnsureStaffAccessAsync(request.ClassId, ct);

            var classExists = await uow.Classes.ClassExistsAsync(request.ClassId, ct);
            if (!classExists)
                throw new NotFoundException("Lớp học không tồn tại.");
        }

        var trimmedName = request.Name.Trim();
        var exists = await uow.Groups.AnyAsync(
            g => g.ClassId == request.ClassId && g.Name.ToLower() == trimmedName.ToLower(), ct);
        if (exists)
            throw new ConflictException($"Nhóm '{trimmedName}' đã tồn tại trong lớp học này.");

        var group = new Group
        {
            Id = Guid.NewGuid(),
            ClassId = request.ClassId,
            Name = trimmedName,
            TopicName = request.TopicName?.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsLocked = false,
            IsApproved = true
        };

        await uow.Groups.AddAsync(group, ct);

        // Nếu sinh viên tạo, tự động thêm sinh viên đó làm Leader đầu tiên của nhóm
        if (!string.IsNullOrEmpty(creatorStudentCode))
        {
            var leaderMember = new GroupMember
            {
                GroupId = group.Id,
                StudentCode = creatorStudentCode,
                Role = "Leader",
                JoinedAt = DateTime.UtcNow
            };
            await uow.Groups.AddMemberAsync(leaderMember, ct);

            // Tự động hủy các lời mời khác gửi tới sinh viên này trong cùng lớp
            await uow.Groups.CancelPendingInvitationsForStudentInClassAsync(request.ClassId, creatorStudentCode, ct);
        }

        await uow.SaveChangesAsync(ct);

        return new GroupDto(
            group.Id,
            group.ClassId,
            group.Name,
            group.TopicName,
            group.CreatedAt,
            !string.IsNullOrEmpty(creatorStudentCode) ? 1 : 0);
    }

    public async Task<GroupDto> UpdateAsync(Guid id, UpdateGroupRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureStaffOrMemberAccessAsync(group.ClassId, group.Id, ct);

        if (!currentUser.IsAdmin && currentUser.Role != UserRole.Lecturer)
        {
            var groupMilestone = await uow.Milestones.GetClassMilestoneByTypeAsync(group.ClassId, MilestoneType.GroupRegistration, ct);
            if (groupMilestone != null && groupMilestone.IsLocked)
                throw new BadRequestException("Giai đoạn đăng ký nhóm đã bị khóa, không thể chỉnh sửa thông tin nhóm trực tiếp.");
        }

        var trimmedName = request.Name.Trim();
        var exists = await uow.Groups.AnyAsync(
            g => g.Id != id && g.ClassId == group.ClassId && g.Name.ToLower() == trimmedName.ToLower(), ct);
        if (exists)
            throw new ConflictException($"Nhóm '{trimmedName}' đã tồn tại trong lớp học này.");

        group.Name = trimmedName;
        group.TopicName = request.TopicName?.Trim();

        uow.Groups.Update(group);
        await uow.SaveChangesAsync(ct);

        var memberCount = (await uow.Groups.GetGroupMembersAsync(id, ct)).Count;

        return new GroupDto(
            group.Id,
            group.ClassId,
            group.Name,
            group.TopicName,
            group.CreatedAt,
            memberCount);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureStaffAccessAsync(group.ClassId, ct);

        uow.Groups.Remove(group);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<List<GroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        return await uow.Groups.GetGroupMembersAsync(groupId, ct);
    }

    public async Task<GroupMemberDto> AddMemberAsync(Guid groupId, AddGroupMemberRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        var targetClass = await uow.Classes.GetByIdAsync(group.ClassId, ct)
            ?? throw new NotFoundException("Lớp học không tồn tại.");

        // Quyền: Leader của nhóm hoặc Staff
        await EnsureLeaderOrStaffAccessAsync(group, ct);

        if (currentUser.Role == UserRole.Student)
        {
            var groupMilestone = await uow.Milestones.GetClassMilestoneByTypeAsync(group.ClassId, MilestoneType.GroupRegistration, ct);
            if (groupMilestone != null && (groupMilestone.IsLocked || DateTime.UtcNow > groupMilestone.Deadline))
                throw new BadRequestException("Giai đoạn đăng ký nhóm đã kết thúc, không thể thêm thành viên trực tiếp.");
        }

        var currentMembers = await uow.Groups.GetGroupMembersAsync(groupId, ct);
        if (currentMembers.Count >= targetClass.MaxGroupMembers && currentUser.Role == UserRole.Student)
            throw new BadRequestException($"Nhóm đã đạt số lượng thành viên tối đa ({targetClass.MaxGroupMembers} người).");

        var studentProfile = await uow.Profiles.GetStudentProfileByCodeAsync(request.StudentCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ sinh viên với mã '{request.StudentCode}'.");

        var isInClass = await uow.Classes.IsStudentOfClassAsync(group.ClassId, Guid.Empty, request.StudentCode, ct);
        if (!isInClass)
            throw new BadRequestException($"Sinh viên '{request.StudentCode}' không thuộc danh sách lớp học này.");

        var alreadyInAnotherGroup = await uow.Groups.IsStudentInAnyGroupInClassAsync(group.ClassId, request.StudentCode, ct);
        if (alreadyInAnotherGroup)
            throw new ConflictException($"Sinh viên '{request.StudentCode}' đã thuộc về một nhóm khác trong lớp.");

        var member = new GroupMember
        {
            GroupId = groupId,
            StudentCode = studentProfile.StudentCode,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "Member" : request.Role.Trim(),
            JoinedAt = DateTime.UtcNow
        };

        await uow.Groups.AddMemberAsync(member, ct);
        await uow.Groups.CancelPendingInvitationsForStudentInClassAsync(group.ClassId, request.StudentCode, ct);
        await uow.SaveChangesAsync(ct);

        return new GroupMemberDto(
            member.GroupId,
            member.StudentCode,
            studentProfile.UserId,
            studentProfile.User?.FullName,
            studentProfile.User?.Email,
            member.Role,
            member.JoinedAt);
    }

    public async Task<GroupMemberDto> UpdateMemberRoleAsync(Guid groupId, string studentCode, UpdateGroupMemberRoleRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureStaffAccessAsync(group.ClassId, ct);

        var member = await uow.Groups.GetMemberAsync(groupId, studentCode, ct)
            ?? throw new NotFoundException($"Thành viên '{studentCode}' không tồn tại trong nhóm này.");

        member.Role = request.Role.Trim();
        await uow.SaveChangesAsync(ct);

        var studentProfile = await uow.Profiles.GetStudentProfileByCodeAsync(studentCode, ct);

        return new GroupMemberDto(
            member.GroupId,
            member.StudentCode,
            studentProfile?.UserId,
            studentProfile?.User?.FullName,
            studentProfile?.User?.Email,
            member.Role,
            member.JoinedAt);
    }

    public async Task RemoveMemberAsync(Guid groupId, string studentCode, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureStaffAccessAsync(group.ClassId, ct);

        var member = await uow.Groups.GetMemberAsync(groupId, studentCode, ct)
            ?? throw new NotFoundException($"Thành viên '{studentCode}' không tồn tại trong nhóm này.");

        uow.Groups.RemoveMember(member);
        await uow.SaveChangesAsync(ct);
    }

    // ==========================================
    // 1. Invitations Workflow
    // ==========================================

    public async Task<GroupInvitationDto> SendInvitationAsync(Guid groupId, SendInvitationRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        var targetClass = await uow.Classes.GetByIdAsync(group.ClassId, ct)
            ?? throw new NotFoundException("Lớp học không tồn tại.");

        await EnsureLeaderOrStaffAccessAsync(group, ct);

        var groupMilestone = await uow.Milestones.GetClassMilestoneByTypeAsync(group.ClassId, MilestoneType.GroupRegistration, ct);
        if (groupMilestone != null)
        {
            if (groupMilestone.IsLocked)
                throw new BadRequestException("Giai đoạn đăng ký nhóm đã bị khóa, không thể gửi thêm lời mời.");

            if (DateTime.UtcNow > groupMilestone.Deadline)
                throw new BadRequestException("Đã hết thời hạn đăng ký/mời thành viên cho lớp học này.");
        }

        var currentMembers = await uow.Groups.GetGroupMembersAsync(groupId, ct);
        if (currentMembers.Count >= targetClass.MaxGroupMembers)
            throw new BadRequestException($"Nhóm đã đủ số lượng tối đa ({targetClass.MaxGroupMembers} người), không thể mời thêm.");

        var studentProfile = await uow.Profiles.GetStudentProfileByCodeAsync(request.StudentCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy sinh viên với mã '{request.StudentCode}'.");

        var isInClass = await uow.Classes.IsStudentOfClassAsync(group.ClassId, Guid.Empty, request.StudentCode, ct);
        if (!isInClass)
            throw new BadRequestException($"Sinh viên '{request.StudentCode}' không thuộc danh sách lớp học này.");

        var alreadyInGroup = await uow.Groups.IsStudentInAnyGroupInClassAsync(group.ClassId, request.StudentCode, ct);
        if (alreadyInGroup)
            throw new ConflictException($"Sinh viên '{request.StudentCode}' đã tham gia một nhóm khác trong lớp.");

        var invitations = await uow.Groups.GetGroupInvitationsAsync(groupId, ct);
        if (invitations.Any(i => i.InvitedStudentCode.Equals(request.StudentCode, StringComparison.OrdinalIgnoreCase) && i.Status == "Pending"))
            throw new ConflictException($"Lời mời tới sinh viên '{request.StudentCode}' đã được gửi trước đó và đang chờ phản hồi.");

        var inv = new GroupInvitation
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            InvitedStudentCode = studentProfile.StudentCode,
            InvitedByUserId = currentUser.UserId,
            Status = GroupInvitationStatus.Pending,
            Message = request.Message?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await uow.Groups.AddInvitationAsync(inv, ct);
        await uow.SaveChangesAsync(ct);

        return new GroupInvitationDto(
            inv.Id,
            inv.GroupId,
            group.Name,
            inv.InvitedStudentCode,
            studentProfile.User?.FullName,
            studentProfile.User?.Email,
            inv.InvitedByUserId,
            currentUser.Email,
            inv.Status.ToString(),
            inv.Message,
            inv.CreatedAt,
            null);
    }

    public async Task<List<GroupInvitationDto>> GetGroupInvitationsAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureStaffOrMemberAccessAsync(group.ClassId, group.Id, ct);
        return await uow.Groups.GetGroupInvitationsAsync(groupId, ct);
    }

    public async Task<List<GroupInvitationDto>> GetStudentInvitationsAsync(CancellationToken ct = default)
    {
        var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
        if (string.IsNullOrEmpty(studentCode)) return [];

        return await uow.Groups.GetStudentInvitationsAsync(studentCode, ct);
    }

    public async Task<GroupMemberDto> RespondInvitationAsync(Guid invitationId, RespondInvitationRequest request, CancellationToken ct = default)
    {
        var inv = await uow.Groups.GetInvitationByIdAsync(invitationId, ct)
            ?? throw new NotFoundException("Lời mời không tồn tại.");

        if (inv.Status != GroupInvitationStatus.Pending)
            throw new BadRequestException("Lời mời này không còn ở trạng thái chờ phản hồi.");

        var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
        if (string.IsNullOrEmpty(studentCode) || !inv.InvitedStudentCode.Equals(studentCode, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Bạn không phải là người nhận của lời mời này.");

        if (!request.Accept)
        {
            inv.Status = GroupInvitationStatus.Rejected;
            inv.RespondedAt = DateTime.UtcNow;
            await uow.SaveChangesAsync(ct);

            return new GroupMemberDto(
                inv.GroupId,
                inv.InvitedStudentCode,
                currentUser.UserId,
                inv.InvitedStudent?.User?.FullName,
                inv.InvitedStudent?.User?.Email,
                "None",
                DateTime.UtcNow);
        }

        var targetClass = inv.Group?.Class;
        if (targetClass != null)
        {
            var groupMilestone = await uow.Milestones.GetClassMilestoneByTypeAsync(targetClass.Id, MilestoneType.GroupRegistration, ct);
            if (groupMilestone != null)
            {
                if (groupMilestone.IsLocked)
                    throw new BadRequestException("Giai đoạn đăng ký nhóm đã bị khóa, không thể chấp nhận lời mời.");

                if (DateTime.UtcNow > groupMilestone.Deadline)
                    throw new BadRequestException("Đã hết thời hạn đăng ký nhóm cho lớp học này.");
            }

            var currentMembers = await uow.Groups.GetGroupMembersAsync(inv.GroupId, ct);
            if (currentMembers.Count >= targetClass.MaxGroupMembers)
                throw new BadRequestException($"Nhóm đã đủ số lượng tối đa ({targetClass.MaxGroupMembers} người).");

            var alreadyInGroup = await uow.Groups.IsStudentInAnyGroupInClassAsync(targetClass.Id, studentCode, ct);
            if (alreadyInGroup)
                throw new ConflictException("Bạn đã là thành viên của một nhóm khác trong lớp này.");
        }

        var member = new GroupMember
        {
            GroupId = inv.GroupId,
            StudentCode = inv.InvitedStudentCode,
            Role = "Member",
            JoinedAt = DateTime.UtcNow
        };

        await uow.Groups.AddMemberAsync(member, ct);

        inv.Status = GroupInvitationStatus.Accepted;
        inv.RespondedAt = DateTime.UtcNow;

        if (targetClass != null)
        {
            await uow.Groups.CancelPendingInvitationsForStudentInClassAsync(targetClass.Id, studentCode, ct);
        }

        await uow.SaveChangesAsync(ct);

        return new GroupMemberDto(
            member.GroupId,
            member.StudentCode,
            currentUser.UserId,
            inv.InvitedStudent?.User?.FullName,
            inv.InvitedStudent?.User?.Email,
            member.Role,
            member.JoinedAt);
    }

    // ==========================================
    // 2. Elect / Transfer Leader
    // ==========================================

    public async Task<GroupMemberDto> ElectLeaderAsync(Guid groupId, ElectLeaderRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureStaffOrMemberAccessAsync(group.ClassId, group.Id, ct);

        if (currentUser.Role == UserRole.Student)
        {
            var groupMilestone = await uow.Milestones.GetClassMilestoneByTypeAsync(group.ClassId, MilestoneType.GroupRegistration, ct);
            if (groupMilestone != null && groupMilestone.IsLocked)
                throw new BadRequestException("Lớp học đã chốt nhóm, việc đổi nhóm trưởng phải thông qua yêu cầu thay đổi (Change Request).");
        }

        var members = await uow.Groups.GetGroupMembersAsync(groupId, ct);
        var targetMember = members.FirstOrDefault(m => m.StudentCode.Equals(request.NewLeaderStudentCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException($"Sinh viên '{request.NewLeaderStudentCode}' không phải là thành viên trong nhóm.");

        // Hoán đổi: thành viên được chọn lên làm Leader, các thành viên khác chuyển thành Member
        foreach (var m in members)
        {
            var dbMember = await uow.Groups.GetMemberAsync(groupId, m.StudentCode, ct);
            if (dbMember != null)
            {
                dbMember.Role = dbMember.StudentCode.Equals(request.NewLeaderStudentCode, StringComparison.OrdinalIgnoreCase)
                    ? "Leader"
                    : "Member";
            }
        }

        await uow.SaveChangesAsync(ct);

        return new GroupMemberDto(
            groupId,
            targetMember.StudentCode,
            targetMember.UserId,
            targetMember.FullName,
            targetMember.Email,
            "Leader",
            DateTime.UtcNow);
    }

    // ==========================================
    // 3. Discovery & Student Dashboard
    // ==========================================

    public async Task<List<AvailableStudentDto>> GetAvailableStudentsInClassAsync(Guid classId, CancellationToken ct = default)
    {
        var allStudents = await uow.Classes.GetAllStudentsInClassAsync(classId, ct);
        var available = new List<AvailableStudentDto>();

        foreach (var st in allStudents)
        {
            var inGroup = await uow.Groups.IsStudentInAnyGroupInClassAsync(classId, st.StudentCode, ct);
            if (!inGroup)
            {
                available.Add(new AvailableStudentDto(
                    st.StudentCode,
                    st.UserId,
                    st.FullName,
                    st.Email));
            }
        }

        return available;
    }

    public async Task<List<GroupDto>> GetMyGroupsAsync(CancellationToken ct = default)
    {
        var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
        if (string.IsNullOrEmpty(studentCode)) return [];

        var groups = await uow.Groups.GetGroupsByStudentCodeAsync(studentCode, ct);
        return groups.Select(g => new GroupDto(
            g.Id,
            g.ClassId,
            g.Name,
            g.TopicName,
            g.CreatedAt,
            g.Members.Count)).ToList();
    }

    // ==========================================
    // 4. Lock & Auto-Assign
    // ==========================================

    public async Task LockGroupRegistrationAsync(Guid classId, ClassGroupLockRequest request, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(classId, ct);

        var classExists = await uow.Classes.ClassExistsAsync(classId, ct);
        if (!classExists)
            throw new NotFoundException("Lớp học không tồn tại.");

        var milestone = await uow.Milestones.GetClassMilestoneByTypeAsync(classId, MilestoneType.GroupRegistration, ct);
        if (milestone == null)
        {
            milestone = new Milestone
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                Title = "Đăng ký nhóm",
                Description = "Thời hạn đăng ký và chốt danh sách nhóm đồ án",
                Type = MilestoneType.GroupRegistration,
                Deadline = request.RegistrationDeadline ?? DateTime.UtcNow.AddDays(7),
                IsLocked = request.IsLocked,
                OrderIndex = 1,
                CreatedAt = DateTime.UtcNow
            };
            await uow.Milestones.AddAsync(milestone, ct);
        }
        else
        {
            milestone.IsLocked = request.IsLocked;
            if (request.RegistrationDeadline.HasValue)
            {
                milestone.Deadline = request.RegistrationDeadline.Value;
            }
            milestone.UpdatedAt = DateTime.UtcNow;
            uow.Milestones.Update(milestone);
        }

        await uow.SaveChangesAsync(ct);
    }

    public async Task<AutoAssignGroupResultDto> AutoAssignUnassignedStudentsAsync(Guid classId, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(classId, ct);

        var targetClass = await uow.Classes.GetByIdAsync(classId, ct)
            ?? throw new NotFoundException("Lớp học không tồn tại.");

        var unassigned = await GetAvailableStudentsInClassAsync(classId, ct);
        if (unassigned.Count == 0)
        {
            return new AutoAssignGroupResultDto(0, 0, ["Tất cả sinh viên trong lớp đều đã có nhóm."]);
        }

        int assignedCount = 0;
        int createdGroupsCount = 0;
        var details = new List<string>();

        // 1. Tìm các nhóm hiện có chưa đủ MaxGroupMembers
        var classGroups = await uow.Groups.FindAsync(g => g.ClassId == classId, asNoTracking: false, ct);
        var unassignedQueue = new Queue<AvailableStudentDto>(unassigned);

        foreach (var group in classGroups)
        {
            if (unassignedQueue.Count == 0) break;
            var members = await uow.Groups.GetGroupMembersAsync(group.Id, ct);
            var slotsAvailable = targetClass.MaxGroupMembers - members.Count;

            while (slotsAvailable > 0 && unassignedQueue.Count > 0)
            {
                var student = unassignedQueue.Dequeue();
                await uow.Groups.AddMemberAsync(new GroupMember
                {
                    GroupId = group.Id,
                    StudentCode = student.StudentCode,
                    Role = "Member",
                    JoinedAt = DateTime.UtcNow
                }, ct);

                assignedCount++;
                slotsAvailable--;
                details.Add($"Đã xếp sinh viên {student.StudentCode} ({student.FullName}) vào nhóm '{group.Name}'.");
            }
        }

        // 2. Nếu vẫn còn sinh viên thừa, gom thành các nhóm mới (IsApproved = false để giảng viên duyệt)
        int newGroupIndex = 1;
        while (unassignedQueue.Count > 0)
        {
            var newGroup = new Group
            {
                Id = Guid.NewGuid(),
                ClassId = classId,
                Name = $"Nhóm ghép {newGroupIndex++}",
                CreatedAt = DateTime.UtcNow,
                IsLocked = false,
                IsApproved = false // Chờ giảng viên xem xét và duyệt theo yêu cầu!
            };

            await uow.Groups.AddAsync(newGroup, ct);
            createdGroupsCount++;

            int countInThisGroup = 0;
            bool leaderAssigned = false;

            while (countInThisGroup < targetClass.MaxGroupMembers && unassignedQueue.Count > 0)
            {
                var student = unassignedQueue.Dequeue();
                await uow.Groups.AddMemberAsync(new GroupMember
                {
                    GroupId = newGroup.Id,
                    StudentCode = student.StudentCode,
                    Role = !leaderAssigned ? "Leader" : "Member",
                    JoinedAt = DateTime.UtcNow
                }, ct);

                leaderAssigned = true;
                countInThisGroup++;
                assignedCount++;
                details.Add($"Đã xếp sinh viên {student.StudentCode} ({student.FullName}) vào nhóm mới '{newGroup.Name}' (Chờ giảng viên duyệt).");
            }
        }

        await uow.SaveChangesAsync(ct);

        return new AutoAssignGroupResultDto(assignedCount, createdGroupsCount, details);
    }
}
