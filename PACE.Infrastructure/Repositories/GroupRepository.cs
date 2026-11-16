using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class GroupRepository(AppDbContext context) : GenericRepository<Group>(context), IGroupRepository
{
    public async Task<PagedResult<GroupDto>> SearchGroupsAsync(GroupQuery query, CancellationToken ct = default)
    {
        var q = DbSet.AsNoTracking();

        if (query.ClassId.HasValue)
        {
            q = q.Where(g => g.ClassId == query.ClassId.Value);
        }

        var search = NormalizeSearch(query.SearchTerm);
        if (!string.IsNullOrEmpty(search))
        {
            q = q.Where(g => g.Name.ToLower().Contains(search) || (g.TopicName != null && g.TopicName.ToLower().Contains(search)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GroupDto(
                g.Id,
                g.ClassId,
                g.Name,
                g.TopicName,
                g.CreatedAt,
                g.Members.Count))
            .ToListAsync(ct);

        return new PagedResult<GroupDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<GroupDetailDto?> GetGroupDetailAsync(Guid id, CancellationToken ct = default)
    {
        var group = await DbSet.AsNoTracking()
            .Include(g => g.Class)
            .Include(g => g.Members)
                .ThenInclude(m => m.StudentProfile)
                    .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (group == null) return null;

        var members = group.Members
            .Select(m => new GroupMemberDto(
                m.GroupId,
                m.StudentCode,
                m.StudentProfile?.UserId,
                m.StudentProfile?.User?.FullName,
                m.StudentProfile?.User?.Email,
                m.Role,
                m.JoinedAt))
            .ToList();

        return new GroupDetailDto(
            group.Id,
            group.ClassId,
            group.Class?.Name ?? "",
            group.Name,
            group.TopicName,
            group.CreatedAt,
            members);
    }

    public async Task<List<GroupMemberDto>> GetGroupMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        return await Context.GroupMembers.AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .Include(m => m.StudentProfile)
                .ThenInclude(sp => sp.User)
            .Select(m => new GroupMemberDto(
                m.GroupId,
                m.StudentCode,
                m.StudentProfile.UserId,
                m.StudentProfile.User.FullName,
                m.StudentProfile.User.Email,
                m.Role,
                m.JoinedAt))
            .ToListAsync(ct);
    }

    public async Task<GroupMember?> GetMemberAsync(Guid groupId, string studentCode, CancellationToken ct = default)
    {
        return await Context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.StudentCode.ToLower() == studentCode.ToLower(), ct);
    }

    public async Task AddMemberAsync(GroupMember member, CancellationToken ct = default)
    {
        await Context.GroupMembers.AddAsync(member, ct);
    }

    public void RemoveMember(GroupMember member)
    {
        Context.GroupMembers.Remove(member);
    }
}
