using System.Text.Json;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class GroupChangeRequestService(
    IUnitOfWork uow,
    ICurrentUserService currentUser) : IGroupChangeRequestService
{
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

    private async Task EnsureMemberOrStaffAccessAsync(Group group, CancellationToken ct)
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
                var isMember = await uow.Groups.GetMemberAsync(group.Id, studentCode, ct);
                if (isMember != null) return;
            }
        }

        throw new ForbiddenException("Bạn không phải thành viên của nhóm này.");
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

    public async Task<GroupChangeRequestDto> CreateTopicChangeRequestAsync(Guid groupId, CreateTopicChangeRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureLeaderOrStaffAccessAsync(group, ct);

        // Kiểm tra đề tài nhóm đã được duyệt hay chưa
        var project = await uow.Groups.GetProjectByGroupIdAsync(groupId, ct);
        if (project?.IsApproved != true)
        {
            throw new BadRequestException("Đề tài của nhóm chưa được duyệt. Bạn có thể tự do đề xuất hoặc chọn đề tài khác mà không cần gửi đơn yêu cầu thay đổi.");
        }

        // Kiểm tra xem đã có đơn đổi đề tài đang chờ duyệt không
        var pendingRequests = await uow.Groups.GetGroupChangeRequestsAsync(groupId, ct);
        if (pendingRequests.Any(r => r.Type == GroupChangeRequestType.ChangeTopic.ToString() && r.Status == GroupChangeRequestStatus.Pending.ToString()))
        {
            throw new ConflictException("Nhóm đã có một yêu cầu đổi đề tài đang chờ giảng viên xem xét.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            newTopicName = request.NewTopicName.Trim(),
            newRepoUrl = request.NewRepoUrl?.Trim()
        });

        var changeRequest = new GroupChangeRequest
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Type = GroupChangeRequestType.ChangeTopic,
            Status = GroupChangeRequestStatus.Pending,
            Title = $"Xin đổi đề tài sang: {request.NewTopicName.Trim()}",
            Reason = request.Reason.Trim(),
            PayloadJson = payload,
            CreatedByUserId = currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await uow.Groups.AddChangeRequestAsync(changeRequest, ct);
        await uow.SaveChangesAsync(ct);

        return (await GetChangeRequestByIdAsync(changeRequest.Id, ct))!;
    }

    public async Task<GroupChangeRequestDto> CreateMemberChangeRequestAsync(Guid groupId, CreateMemberChangeRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        var actionUpper = request.Action.Trim().ToUpperInvariant();
        if (actionUpper != "ADD" && actionUpper != "REMOVE")
        {
            throw new BadRequestException("Hành động thay đổi thành viên chỉ chấp nhận 'ADD' hoặc 'REMOVE'.");
        }

        var studentProfile = await uow.Profiles.GetStudentProfileByCodeAsync(request.StudentCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy sinh viên có mã '{request.StudentCode}'.");

        var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);

        if (actionUpper == "REMOVE")
        {
            // Cho phép: bản thân sinh viên xin rút KHỎI NHÓM, hoặc Leader xin loại thành viên
            var isSelfRequest = !string.IsNullOrEmpty(studentCode) && studentCode.Equals(request.StudentCode, StringComparison.OrdinalIgnoreCase);
            if (!isSelfRequest)
            {
                await EnsureLeaderOrStaffAccessAsync(group, ct);
            }

            var isMember = await uow.Groups.GetMemberAsync(groupId, request.StudentCode, ct);
            if (isMember == null)
            {
                throw new BadRequestException($"Sinh viên '{request.StudentCode}' không phải là thành viên của nhóm.");
            }
        }
        else // ADD
        {
            await EnsureLeaderOrStaffAccessAsync(group, ct);

            var targetClass = await uow.Classes.GetByIdAsync(group.ClassId, ct);
            var currentMembers = await uow.Groups.GetGroupMembersAsync(groupId, ct);
            if (targetClass != null && currentMembers.Count >= targetClass.MaxGroupMembers)
            {
                throw new BadRequestException($"Nhóm đã đạt số lượng tối đa ({targetClass.MaxGroupMembers} người).");
            }

            var isInClass = await uow.Classes.IsStudentOfClassAsync(group.ClassId, Guid.Empty, request.StudentCode, ct);
            if (!isInClass)
            {
                throw new BadRequestException($"Sinh viên '{request.StudentCode}' không thuộc lớp học này.");
            }

            var inAnotherGroup = await uow.Groups.IsStudentInAnyGroupInClassAsync(group.ClassId, request.StudentCode, ct);
            if (inAnotherGroup)
            {
                throw new ConflictException($"Sinh viên '{request.StudentCode}' đã tham gia một nhóm khác trong lớp.");
            }
        }

        var reqType = actionUpper == "ADD" ? GroupChangeRequestType.AddMember : GroupChangeRequestType.RemoveMember;
        var pendingRequests = await uow.Groups.GetGroupChangeRequestsAsync(groupId, ct);
        if (pendingRequests.Any(r => r.Type == reqType.ToString() && r.Status == GroupChangeRequestStatus.Pending.ToString() && r.PayloadJson.Contains(request.StudentCode)))
        {
            throw new ConflictException($"Yêu cầu cho sinh viên '{request.StudentCode}' đã tồn tại và đang chờ phê duyệt.");
        }

        var titleText = actionUpper == "ADD"
            ? $"Xin bổ sung thành viên: {studentProfile.User?.FullName ?? request.StudentCode} ({request.StudentCode})"
            : $"Xin rút/loại thành viên: {studentProfile.User?.FullName ?? request.StudentCode} ({request.StudentCode})";

        var payload = JsonSerializer.Serialize(new
        {
            studentCode = request.StudentCode.Trim(),
            action = actionUpper
        });

        var changeRequest = new GroupChangeRequest
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Type = reqType,
            Status = GroupChangeRequestStatus.Pending,
            Title = titleText,
            Reason = request.Reason.Trim(),
            PayloadJson = payload,
            CreatedByUserId = currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await uow.Groups.AddChangeRequestAsync(changeRequest, ct);
        await uow.SaveChangesAsync(ct);

        return (await GetChangeRequestByIdAsync(changeRequest.Id, ct))!;
    }

    public async Task<GroupChangeRequestDto> CreateLeaderChangeRequestAsync(Guid groupId, CreateLeaderChangeRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureMemberOrStaffAccessAsync(group, ct);

        var member = await uow.Groups.GetMemberAsync(groupId, request.NewLeaderStudentCode, ct);
        if (member == null)
            throw new NotFoundException($"Sinh viên '{request.NewLeaderStudentCode}' không phải là thành viên của nhóm.");

        if (member.Role.Equals("Leader", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException($"Sinh viên '{request.NewLeaderStudentCode}' hiện tại đã là nhóm trưởng.");

        var payload = JsonSerializer.Serialize(new
        {
            newLeaderStudentCode = request.NewLeaderStudentCode.Trim()
        });

        var changeRequest = new GroupChangeRequest
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Type = GroupChangeRequestType.ChangeLeader,
            Status = GroupChangeRequestStatus.Pending,
            Title = $"Xin chuyển giao vai trò nhóm trưởng cho: {request.NewLeaderStudentCode.Trim()}",
            Reason = request.Reason.Trim(),
            PayloadJson = payload,
            CreatedByUserId = currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await uow.Groups.AddChangeRequestAsync(changeRequest, ct);
        await uow.SaveChangesAsync(ct);

        return (await GetChangeRequestByIdAsync(changeRequest.Id, ct))!;
    }

    public async Task<List<GroupChangeRequestDto>> GetGroupChangeRequestsAsync(Guid groupId, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureMemberOrStaffAccessAsync(group, ct);
        return await uow.Groups.GetGroupChangeRequestsAsync(groupId, ct);
    }

    public async Task<GroupChangeRequestDto> GetChangeRequestByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var r = await uow.Groups.GetChangeRequestByIdAsync(requestId, ct)
            ?? throw new NotFoundException("Yêu cầu thay đổi không tồn tại.");

        return new GroupChangeRequestDto(
            r.Id,
            r.GroupId,
            r.Group?.Name ?? "",
            r.Type.ToString(),
            r.Status.ToString(),
            r.Title,
            r.Reason,
            r.PayloadJson,
            r.FailStudentIfRemoved,
            r.CreatedByUserId,
            r.CreatedByUser?.FullName,
            r.CreatedAt,
            r.ReviewedByUserId,
            r.ReviewedByUser?.FullName,
            r.ReviewNotes,
            r.ReviewedAt);
    }

    public async Task CancelChangeRequestAsync(Guid groupId, Guid requestId, CancellationToken ct = default)
    {
        var r = await uow.Groups.GetChangeRequestByIdAsync(requestId, ct)
            ?? throw new NotFoundException("Yêu cầu thay đổi không tồn tại.");

        if (r.GroupId != groupId)
            throw new BadRequestException("Yêu cầu không thuộc nhóm này.");

        if (r.Status != GroupChangeRequestStatus.Pending)
            throw new BadRequestException("Chỉ có thể hủy yêu cầu khi đang ở trạng thái chờ duyệt (Pending).");

        if (r.CreatedByUserId != currentUser.UserId && !currentUser.IsAdmin && currentUser.Role != UserRole.Lecturer)
            throw new ForbiddenException("Bạn không có quyền hủy yêu cầu này.");

        r.Status = GroupChangeRequestStatus.Cancelled;
        await uow.SaveChangesAsync(ct);
    }

    public async Task<List<GroupChangeRequestDto>> GetClassChangeRequestsAsync(Guid classId, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(classId, ct);
        return await uow.Groups.GetClassChangeRequestsAsync(classId, ct);
    }

    public async Task<GroupChangeRequestDto> ReviewChangeRequestAsync(Guid groupId, Guid requestId, ReviewChangeRequest request, CancellationToken ct = default)
    {
        var r = await uow.Groups.GetChangeRequestByIdAsync(requestId, ct)
            ?? throw new NotFoundException("Yêu cầu thay đổi không tồn tại.");

        if (r.GroupId != groupId)
            throw new BadRequestException("Yêu cầu không thuộc nhóm này.");

        var group = r.Group ?? await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        await EnsureStaffAccessAsync(group.ClassId, ct);

        if (r.Status != GroupChangeRequestStatus.Pending)
            throw new BadRequestException("Yêu cầu này đã được xử lý trước đó.");

        r.ReviewedByUserId = currentUser.UserId;
        r.ReviewNotes = request.Notes?.Trim();
        r.ReviewedAt = DateTime.UtcNow;
        r.FailStudentIfRemoved = request.FailStudentIfRemoved;

        if (!request.IsApproved)
        {
            r.Status = GroupChangeRequestStatus.Rejected;
            await uow.SaveChangesAsync(ct);
            return (await GetChangeRequestByIdAsync(requestId, ct))!;
        }

        r.Status = GroupChangeRequestStatus.Approved;

        // Auto-Apply logic
        using var jsonDoc = JsonDocument.Parse(r.PayloadJson);
        var root = jsonDoc.RootElement;

        switch (r.Type)
        {
            case GroupChangeRequestType.ChangeTopic:
                if (root.TryGetProperty("newTopicName", out var topicElem))
                {
                    var newTopic = topicElem.GetString();
                    if (!string.IsNullOrWhiteSpace(newTopic))
                    {
                        group.TopicName = newTopic.Trim();
                        group.CourseTopicId = null;

                        string? newRepo = null;
                        if (root.TryGetProperty("newRepoUrl", out var repoElem))
                        {
                            newRepo = repoElem.GetString();
                        }

                        var project = await uow.Groups.UpsertProjectForGroupAsync(groupId, group.Class.CourseId, newTopic.Trim(), newRepo, ct);
                        project.IsApproved = true; // Giảng viên đã trực tiếp phê duyệt đơn đổi đề tài
                        project.Notes = $"Đã chấp thuận đổi đề tài: {newTopic.Trim()}";
                        project.DecidedAt = DateTime.UtcNow;

                        await uow.Groups.AddAuditEventAsync(
                            currentUser.UserId,
                            currentUser.Email,
                            project.AnalysisRunId,
                            "TOPIC_CHANGED",
                            $"Giảng viên duyệt đổi đề tài thành: {newTopic.Trim()}",
                            ct);
                    }
                }
                break;

            case GroupChangeRequestType.AddMember:
                if (root.TryGetProperty("studentCode", out var addCodeElem))
                {
                    var studentCode = addCodeElem.GetString();
                    if (!string.IsNullOrWhiteSpace(studentCode))
                    {
                        var member = new GroupMember
                        {
                            GroupId = groupId,
                            StudentCode = studentCode.Trim(),
                            Role = "Member",
                            JoinedAt = DateTime.UtcNow
                        };
                        await uow.Groups.AddMemberAsync(member, ct);
                        await uow.Groups.CancelPendingInvitationsForStudentInClassAsync(group.ClassId, studentCode.Trim(), ct);

                        await uow.Groups.AddAuditEventAsync(
                            currentUser.UserId,
                            currentUser.Email,
                            groupId,
                            "MEMBER_ADDED",
                            $"Giảng viên duyệt thêm sinh viên {studentCode.Trim()} vào nhóm",
                            ct);
                    }
                }
                break;

            case GroupChangeRequestType.RemoveMember:
                if (root.TryGetProperty("studentCode", out var removeCodeElem))
                {
                    var studentCode = removeCodeElem.GetString();
                    if (!string.IsNullOrWhiteSpace(studentCode))
                    {
                        var member = await uow.Groups.GetMemberAsync(groupId, studentCode.Trim(), ct);
                        if (member != null)
                        {
                            uow.Groups.RemoveMember(member);
                        }

                        // Nếu giảng viên chọn đánh dấu sinh viên rớt môn
                        if (request.FailStudentIfRemoved)
                        {
                            await uow.Groups.UpdateStudentClassStatusAsync(group.ClassId, studentCode.Trim(), 0, ct);
                        }

                        await uow.Groups.AddAuditEventAsync(
                            currentUser.UserId,
                            currentUser.Email,
                            groupId,
                            "MEMBER_REMOVED",
                            $"Giảng viên duyệt rút sinh viên {studentCode.Trim()} khỏi nhóm" + (request.FailStudentIfRemoved ? " và đánh dấu rớt môn (Status = 0)" : ""),
                            ct);
                    }
                }
                break;

            case GroupChangeRequestType.ChangeLeader:
                if (root.TryGetProperty("newLeaderStudentCode", out var leaderCodeElem))
                {
                    var newLeaderCode = leaderCodeElem.GetString();
                    if (!string.IsNullOrWhiteSpace(newLeaderCode))
                    {
                        var members = await uow.Groups.GetGroupMembersAsync(groupId, ct);
                        foreach (var m in members)
                        {
                            var dbMember = await uow.Groups.GetMemberAsync(groupId, m.StudentCode, ct);
                            if (dbMember != null)
                            {
                                dbMember.Role = dbMember.StudentCode.Equals(newLeaderCode.Trim(), StringComparison.OrdinalIgnoreCase)
                                    ? "Leader"
                                    : "Member";
                            }
                        }

                        await uow.Groups.AddAuditEventAsync(
                            currentUser.UserId,
                            currentUser.Email,
                            groupId,
                            "LEADER_CHANGED",
                            $"Giảng viên duyệt đổi nhóm trưởng sang sinh viên {newLeaderCode.Trim()}",
                            ct);
                    }
                }
                break;
        }

        await uow.SaveChangesAsync(ct);
        return (await GetChangeRequestByIdAsync(requestId, ct))!;
    }
}
