using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface IGroupRepository : IGenericRepository<Group>
{
    Task<PagedResult<GroupDto>> SearchGroupsAsync(GroupQuery query, CancellationToken ct = default);
    Task<GroupDetailDto?> GetGroupDetailAsync(Guid id, CancellationToken ct = default);
    Task<List<GroupMemberDto>> GetGroupMembersAsync(Guid groupId, CancellationToken ct = default);
    Task<GroupMember?> GetMemberAsync(Guid groupId, string studentCode, CancellationToken ct = default);
    Task AddMemberAsync(GroupMember member, CancellationToken ct = default);
    void RemoveMember(GroupMember member);
}
