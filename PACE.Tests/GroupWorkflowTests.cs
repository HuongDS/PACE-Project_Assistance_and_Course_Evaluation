using System.Linq.Expressions;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using Xunit;

namespace PACE.Tests;

public class GroupWorkflowTests
{
    private static ICurrentUserService MockUser(UserRole role, Guid userId, string email = "user@pace.edu")
    {
        return Stub<ICurrentUserService>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_IsAuthenticated" => true,
                "get_Role" => role,
                "get_UserId" => userId,
                "get_Email" => email,
                "get_IsAdmin" => role == UserRole.Admin,
                _ => null!
            };
            return res;
        });
    }

    [Fact]
    public async Task Student_Can_Create_Group_And_Becomes_Leader()
    {
        var classId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var studentCode = "SE170001";
        var user = MockUser(UserRole.Student, studentUserId);

        var targetClass = new Class
        {
            Id = classId,
            Name = "SE1701",
            IsGroupLocked = false,
            MaxGroupMembers = 5
        };

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Class?>(targetClass),
                "GetStudentCodeByUserIdAsync" => Task.FromResult<string?>(studentCode),
                "IsStudentOfClassAsync" => Task.FromResult(true),
                _ => null!
            };
            return res;
        });

        Group? addedGroup = null;
        GroupMember? addedMember = null;

        var groupRepo = Stub<IGroupRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "IsStudentInAnyGroupInClassAsync" => Task.FromResult(false),
                "AnyAsync" => Task.FromResult(false),
                "AddAsync" => Task.Run(() => { addedGroup = (Group)a[0]; }),
                "AddMemberAsync" => Task.Run(() => { addedMember = (GroupMember)a[0]; }),
                "CancelPendingInvitationsForStudentInClassAsync" => Task.CompletedTask,
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Classes" => classRepo,
                "get_Groups" => groupRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var groupService = new GroupService(uow, user);

        var request = new CreateGroupRequest
        {
            ClassId = classId,
            Name = "Nhóm Chiến Binh",
            TopicName = "Website Bán Hàng"
        };

        var result = await groupService.CreateAsync(request);

        Assert.NotNull(result);
        Assert.Equal("Nhóm Chiến Binh", result.Name);
        Assert.NotNull(addedGroup);
        Assert.NotNull(addedMember);
        Assert.Equal("Leader", addedMember!.Role);
        Assert.Equal(studentCode, addedMember.StudentCode);
    }

    [Fact]
    public async Task Student_Already_In_Group_Cannot_Create_Second_Group()
    {
        var classId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var user = MockUser(UserRole.Student, studentUserId);

        var targetClass = new Class { Id = classId, IsGroupLocked = false };

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Class?>(targetClass),
                "GetStudentCodeByUserIdAsync" => Task.FromResult<string?>("SE170001"),
                "IsStudentOfClassAsync" => Task.FromResult(true),
                _ => null!
            };
            return res;
        });

        var groupRepo = Stub<IGroupRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "IsStudentInAnyGroupInClassAsync" => Task.FromResult(true), // Đã có nhóm!
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Classes" => classRepo,
                "get_Groups" => groupRepo,
                _ => null!
            };
            return res;
        });

        var service = new GroupService(uow, user);
        var request = new CreateGroupRequest { ClassId = classId, Name = "Nhóm 2" };

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task Student_Cannot_Create_Group_When_Class_Is_Locked()
    {
        var classId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var user = MockUser(UserRole.Student, studentUserId);

        var targetClass = new Class { Id = classId, IsGroupLocked = true }; // Bị khóa!

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Class?>(targetClass),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Classes" => classRepo,
                _ => null!
            };
            return res;
        });

        var service = new GroupService(uow, user);
        var request = new CreateGroupRequest { ClassId = classId, Name = "Nhóm Mới" };

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task Student_Cannot_Create_Group_When_Registration_Deadline_Passed()
    {
        var classId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var user = MockUser(UserRole.Student, studentUserId);

        var targetClass = new Class
        {
            Id = classId,
            IsGroupLocked = false,
            GroupRegistrationDeadline = DateTime.UtcNow.AddDays(-1) // Đã quá hạn 1 ngày!
        };

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Class?>(targetClass),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Classes" => classRepo,
                _ => null!
            };
            return res;
        });

        var service = new GroupService(uow, user);
        var request = new CreateGroupRequest { ClassId = classId, Name = "Nhóm Muộn" };

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task Leader_Can_Elect_New_Leader()
    {
        var groupId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var currentLeaderId = Guid.NewGuid();
        var user = MockUser(UserRole.Student, currentLeaderId);

        var group = new Group { Id = groupId, ClassId = classId, Name = "Nhóm Test" };
        var targetClass = new Class { Id = classId, IsGroupLocked = false };

        var member1 = new GroupMember { GroupId = groupId, StudentCode = "SE170001", Role = "Leader" };
        var member2 = new GroupMember { GroupId = groupId, StudentCode = "SE170002", Role = "Member" };

        var groupRepo = Stub<IGroupRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Group?>(group),
                "GetGroupMembersAsync" => Task.FromResult(new List<GroupMemberDto>
                {
                    new(groupId, "SE170001", currentLeaderId, "Nguyen Van A", "a@pace.edu", "Leader", DateTime.UtcNow),
                    new(groupId, "SE170002", Guid.NewGuid(), "Tran Van B", "b@pace.edu", "Member", DateTime.UtcNow)
                }),
                "GetMemberAsync" => Task.FromResult<GroupMember?>(
                    (string)a[1] == "SE170001" ? member1 : member2),
                _ => null!
            };
            return res;
        });

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Class?>(targetClass),
                "GetStudentCodeByUserIdAsync" => Task.FromResult<string?>("SE170001"),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Groups" => groupRepo,
                "get_Classes" => classRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var service = new GroupService(uow, user);
        var result = await service.ElectLeaderAsync(groupId, new ElectLeaderRequest { NewLeaderStudentCode = "SE170002" });

        Assert.NotNull(result);
        Assert.Equal("Leader", result.Role);
        Assert.Equal("SE170002", result.StudentCode);
        Assert.Equal("Leader", member2.Role);
        Assert.Equal("Member", member1.Role);
    }

    [Fact]
    public async Task Cannot_Propose_Topic_Directly_When_Already_Approved()
    {
        var groupId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var leaderUserId = Guid.NewGuid();
        var user = MockUser(UserRole.Student, leaderUserId);

        var group = new Group { Id = groupId, ClassId = classId, Name = "Nhóm Test" };
        var targetClass = new Class { Id = classId, CourseId = courseId };

        var approvedProject = new CourseProject
        {
            CourseId = courseId,
            AnalysisRunId = Guid.NewGuid(),
            IsApproved = true // Đã duyệt!
        };

        var groupRepo = Stub<IGroupRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Group?>(group),
                "GetMemberAsync" => Task.FromResult<GroupMember?>(new GroupMember { GroupId = groupId, StudentCode = "SE170001", Role = "Leader" }),
                "GetProjectByGroupIdAsync" => Task.FromResult<CourseProject?>(approvedProject),
                _ => null!
            };
            return res;
        });

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Class?>(targetClass),
                "GetStudentCodeByUserIdAsync" => Task.FromResult<string?>("SE170001"),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Groups" => groupRepo,
                "get_Classes" => classRepo,
                _ => null!
            };
            return res;
        });

        var topicService = new TopicManagementService(uow, user);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            topicService.ProposeTopicAsync(groupId, new ProposeTopicRequest { TopicName = "Đề tài mới tùy tiện" }));
    }

    [Fact]
    public async Task ChangeRequest_Topic_Lecturer_Approve_AutoApplies()
    {
        var groupId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var lecturerUserId = Guid.NewGuid();
        var lecturer = MockUser(UserRole.Lecturer, lecturerUserId);

        var group = new Group
        {
            Id = groupId,
            ClassId = classId,
            Name = "Nhóm 01",
            TopicName = "Đề tài cũ",
            Class = new Class { Id = classId, CourseId = courseId }
        };

        var existingProject = new CourseProject
        {
            CourseId = courseId,
            AnalysisRunId = Guid.NewGuid(),
            IsApproved = true
        };

        var changeRequest = new GroupChangeRequest
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Group = group,
            Type = GroupChangeRequestType.ChangeTopic,
            Status = GroupChangeRequestStatus.Pending,
            Title = "Xin đổi đề tài",
            Reason = "Công nghệ cũ không phù hợp",
            PayloadJson = "{\"newTopicName\":\"Hệ thống PACE Mới\"}",
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        var groupRepo = Stub<IGroupRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetChangeRequestByIdAsync" => Task.FromResult<GroupChangeRequest?>(changeRequest),
                "GetByIdAsync" => Task.FromResult<Group?>(group),
                "UpsertProjectForGroupAsync" => Task.FromResult(existingProject),
                "AddAuditEventAsync" => Task.CompletedTask,
                _ => null!
            };
            return res;
        });

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "IsLecturerOfClassAsync" => Task.FromResult(true),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Groups" => groupRepo,
                "get_Classes" => classRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var service = new GroupChangeRequestService(uow, lecturer);

        var result = await service.ReviewChangeRequestAsync(
            groupId,
            changeRequest.Id,
            new ReviewChangeRequest { IsApproved = true, Notes = "Đồng ý đổi đề tài" });

        Assert.NotNull(result);
        Assert.Equal("Approved", result.Status);
        Assert.Equal("Hệ thống PACE Mới", group.TopicName);
        Assert.True(existingProject.IsApproved);
    }

    [Fact]
    public async Task ChangeRequest_RemoveMember_With_FailStudent_Marks_Dropped()
    {
        var groupId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var lecturerUserId = Guid.NewGuid();
        var lecturer = MockUser(UserRole.Lecturer, lecturerUserId);

        var group = new Group
        {
            Id = groupId,
            ClassId = classId,
            Name = "Nhóm 01",
            Class = new Class { Id = classId }
        };

        var memberToRemove = new GroupMember { GroupId = groupId, StudentCode = "SE170999", Role = "Member" };

        var changeRequest = new GroupChangeRequest
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Group = group,
            Type = GroupChangeRequestType.RemoveMember,
            Status = GroupChangeRequestStatus.Pending,
            Title = "Xin rút khỏi nhóm",
            Reason = "Bận việc gia đình",
            PayloadJson = "{\"studentCode\":\"SE170999\",\"action\":\"REMOVE\"}",
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        bool memberRemoved = false;
        int updatedStatus = -1;

        var groupRepo = Stub<IGroupRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetChangeRequestByIdAsync" => Task.FromResult<GroupChangeRequest?>(changeRequest),
                "GetByIdAsync" => Task.FromResult<Group?>(group),
                "GetMemberAsync" => Task.FromResult<GroupMember?>(memberToRemove),
                "RemoveMember" => Task.Run(() => { memberRemoved = true; }),
                "UpdateStudentClassStatusAsync" => Task.Run(() => { updatedStatus = (int)a[2]; }),
                "AddAuditEventAsync" => Task.CompletedTask,
                _ => null!
            };
            return res;
        });

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "IsLecturerOfClassAsync" => Task.FromResult(true),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Groups" => groupRepo,
                "get_Classes" => classRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var service = new GroupChangeRequestService(uow, lecturer);

        var result = await service.ReviewChangeRequestAsync(
            groupId,
            changeRequest.Id,
            new ReviewChangeRequest
            {
                IsApproved = true,
                Notes = "Rút sau hạn chốt -> đánh dấu rớt môn",
                FailStudentIfRemoved = true
            });

        Assert.NotNull(result);
        Assert.Equal("Approved", result.Status);
        Assert.True(memberRemoved);
        Assert.Equal(0, updatedStatus); // Đã chuyển status = 0 (Rớt môn)!
    }
}
