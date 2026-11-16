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
}
