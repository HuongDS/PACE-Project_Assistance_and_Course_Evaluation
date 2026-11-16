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
        await EnsureStaffAccessAsync(request.ClassId, ct);

        var classExists = await uow.Classes.ClassExistsAsync(request.ClassId, ct);
        if (!classExists)
            throw new NotFoundException("Lớp học không tồn tại.");

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
            CreatedAt = DateTime.UtcNow
        };

        await uow.Groups.AddAsync(group, ct);
        await uow.SaveChangesAsync(ct);

        return new GroupDto(
            group.Id,
            group.ClassId,
            group.Name,
            group.TopicName,
            group.CreatedAt,
            0);
    }

    public async Task<GroupDto> UpdateAsync(Guid id, UpdateGroupRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureStaffOrMemberAccessAsync(group.ClassId, group.Id, ct);

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

        await EnsureStaffAccessAsync(group.ClassId, ct);

        var studentProfile = await uow.Profiles.GetStudentProfileByCodeAsync(request.StudentCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ sinh viên với mã '{request.StudentCode}'.");

        var isInClass = await uow.Classes.IsStudentOfClassAsync(group.ClassId, Guid.Empty, request.StudentCode, ct);
        if (!isInClass)
            throw new BadRequestException($"Sinh viên '{request.StudentCode}' không thuộc danh sách lớp học này.");

        var existingMember = await uow.Groups.GetMemberAsync(groupId, request.StudentCode, ct);
        if (existingMember != null)
            throw new ConflictException($"Sinh viên '{request.StudentCode}' đã là thành viên của nhóm này.");

        var member = new GroupMember
        {
            GroupId = groupId,
            StudentCode = studentProfile.StudentCode,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "Member" : request.Role.Trim(),
            JoinedAt = DateTime.UtcNow
        };

        await uow.Groups.AddMemberAsync(member, ct);
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
}
