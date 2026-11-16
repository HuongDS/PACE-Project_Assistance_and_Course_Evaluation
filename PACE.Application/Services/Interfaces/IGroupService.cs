using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IGroupService
{
    Task<PagedResult<GroupDto>> GetPagedAsync(GroupQuery query, CancellationToken ct = default);
    Task<GroupDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<GroupDto> CreateAsync(CreateGroupRequest request, CancellationToken ct = default);
    Task<GroupDto> UpdateAsync(Guid id, UpdateGroupRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<List<GroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken ct = default);
    Task<GroupMemberDto> AddMemberAsync(Guid groupId, AddGroupMemberRequest request, CancellationToken ct = default);
    Task<GroupMemberDto> UpdateMemberRoleAsync(Guid groupId, string studentCode, UpdateGroupMemberRoleRequest request, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid groupId, string studentCode, CancellationToken ct = default);

    // ==========================================
    // Workflow: Invitations, Elect Leader & Student Discovery
    // ==========================================
    Task<GroupInvitationDto> SendInvitationAsync(Guid groupId, SendInvitationRequest request, CancellationToken ct = default);
    Task<List<GroupInvitationDto>> GetGroupInvitationsAsync(Guid groupId, CancellationToken ct = default);
    Task<List<GroupInvitationDto>> GetStudentInvitationsAsync(CancellationToken ct = default);
    Task<GroupMemberDto> RespondInvitationAsync(Guid invitationId, RespondInvitationRequest request, CancellationToken ct = default);
    Task<GroupMemberDto> ElectLeaderAsync(Guid groupId, ElectLeaderRequest request, CancellationToken ct = default);
    Task<List<AvailableStudentDto>> GetAvailableStudentsInClassAsync(Guid classId, CancellationToken ct = default);
    Task<List<GroupDto>> GetMyGroupsAsync(CancellationToken ct = default);

    // ==========================================
    // Deadline, Lock & Auto-Assign
    // ==========================================
    Task LockGroupRegistrationAsync(Guid classId, ClassGroupLockRequest request, CancellationToken ct = default);
    Task<AutoAssignGroupResultDto> AutoAssignUnassignedStudentsAsync(Guid classId, CancellationToken ct = default);
}
