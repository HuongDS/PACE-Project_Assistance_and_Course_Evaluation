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

    // Group Member check
    Task<bool> IsStudentInAnyGroupInClassAsync(Guid classId, string studentCode, CancellationToken ct = default);
    Task<Group?> GetGroupByStudentCodeInClassAsync(Guid classId, string studentCode, CancellationToken ct = default);
    Task<List<Group>> GetGroupsByStudentCodeAsync(string studentCode, CancellationToken ct = default);

    // Invitations
    Task<GroupInvitation?> GetInvitationByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<GroupInvitationDto>> GetGroupInvitationsAsync(Guid groupId, CancellationToken ct = default);
    Task<List<GroupInvitationDto>> GetStudentInvitationsAsync(string studentCode, CancellationToken ct = default);
    Task AddInvitationAsync(GroupInvitation invitation, CancellationToken ct = default);
    Task CancelPendingInvitationsForStudentInClassAsync(Guid classId, string studentCode, CancellationToken ct = default);

    // Change Requests
    Task<GroupChangeRequest?> GetChangeRequestByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<GroupChangeRequestDto>> GetGroupChangeRequestsAsync(Guid groupId, CancellationToken ct = default);
    Task<List<GroupChangeRequestDto>> GetClassChangeRequestsAsync(Guid classId, CancellationToken ct = default);
    Task AddChangeRequestAsync(GroupChangeRequest request, CancellationToken ct = default);

    // Topics & Projects
    Task<List<CourseTopicDto>> GetCourseTopicsAsync(Guid courseId, CancellationToken ct = default);
    Task<CourseTopic?> GetCourseTopicByIdAsync(Guid topicId, CancellationToken ct = default);
    Task AddCourseTopicAsync(CourseTopic topic, CancellationToken ct = default);
    void RemoveCourseTopic(CourseTopic topic);
    Task<CourseProject?> GetProjectByGroupIdAsync(Guid groupId, CancellationToken ct = default);
    Task<CourseProject> UpsertProjectForGroupAsync(Guid groupId, Guid courseId, string topicName, string? repoUrl, CancellationToken ct = default);
    Task AddAuditEventAsync(Guid actorId, string actorName, Guid resourceId, string eventType, string details, CancellationToken ct = default);
    Task UpdateStudentClassStatusAsync(Guid classId, string studentCode, int status, CancellationToken ct = default);
}
