using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using Xunit;

namespace PACE.Tests;

/// <summary>Đề tài mẫu mở theo kỳ; số nhóm tối đa tính riêng từng lớp.</summary>
public class CourseTopicSemesterTests
{
    private readonly Guid _course = Guid.NewGuid();
    private readonly Guid _fall = Guid.NewGuid();
    private readonly Guid _summer = Guid.NewGuid();

    private TopicManagementService Service(Group group, Class targetClass, CourseTopic topic)
    {
        var user = Stub<ICurrentUserService>.Create((m, a) => m.Name switch
        {
            "get_IsAdmin" => true,
            "get_Role" => UserRole.Admin,
            "get_UserId" => Guid.NewGuid(),
            "get_Email" => "admin@pace.edu.vn",
            _ => null!
        });
        var groups = Stub<IGroupRepository>.Create((m, a) => m.Name switch
        {
            "GetByIdAsync" => Task.FromResult<Group?>(group),
            "GetProjectByGroupIdAsync" => Task.FromResult<CourseProject?>(null),
            "GetCourseTopicByIdAsync" => Task.FromResult<CourseTopic?>(topic),
            "UpsertProjectForGroupAsync" => Task.FromResult(new CourseProject { CourseId = _course, AnalysisRunId = Guid.NewGuid() }),
            "AddAuditEventAsync" => Task.CompletedTask,
            "GetGroupDetailAsync" => Task.FromResult<GroupDetailDto?>(null),
            _ => null!
        });
        var classes = Stub<IClassRepository>.Create((m, a) => m.Name == "GetByIdAsync" ? Task.FromResult<Class?>(targetClass) : null!);
        var uow = Stub<IUnitOfWork>.Create((m, a) => m.Name switch
        {
            "get_Groups" => groups,
            "get_Classes" => classes,
            "SaveChangesAsync" => Task.FromResult(1),
            _ => null!
        });
        return new TopicManagementService(uow, user);
    }

    private (Group group, Class targetClass, CourseTopic topic) Setup(Guid? topicSemester, int maxGroups = 1)
    {
        var targetClass = new Class { Id = Guid.NewGuid(), Name = "SE1703", CourseId = _course, SemesterId = _fall };
        var group = new Group { Id = Guid.NewGuid(), ClassId = targetClass.Id, Name = "Nhóm 1" };
        var topic = new CourseTopic { Id = Guid.NewGuid(), CourseId = _course, SemesterId = topicSemester, Title = "Đặt sân", MaxGroups = maxGroups };
        return (group, targetClass, topic);
    }

    [Fact]
    public async Task Topic_Full_In_Another_Class_Can_Still_Be_Selected()
    {
        var (group, targetClass, topic) = Setup(_fall);
        topic.Groups.Add(new Group { Id = Guid.NewGuid(), ClassId = Guid.NewGuid(), Name = "Nhóm lớp khác" });

        await Service(group, targetClass, topic).SelectTopicAsync(group.Id, new SelectTopicRequest { TopicId = topic.Id });

        Assert.Equal(topic.Id, group.CourseTopicId);
    }

    [Fact]
    public async Task Topic_Full_In_Own_Class_Is_Rejected()
    {
        var (group, targetClass, topic) = Setup(_fall);
        topic.Groups.Add(new Group { Id = Guid.NewGuid(), ClassId = targetClass.Id, Name = "Nhóm 2" });

        await Assert.ThrowsAsync<ConflictException>(() =>
            Service(group, targetClass, topic).SelectTopicAsync(group.Id, new SelectTopicRequest { TopicId = topic.Id }));
        Assert.Null(group.CourseTopicId);
    }

    [Fact]
    public async Task Group_Keeps_Its_Own_Seat_When_Selecting_Again()
    {
        var (group, targetClass, topic) = Setup(_fall);
        group.CourseTopicId = topic.Id;
        topic.Groups.Add(group);

        await Service(group, targetClass, topic).SelectTopicAsync(group.Id, new SelectTopicRequest { TopicId = topic.Id });

        Assert.Equal(topic.Id, group.CourseTopicId);
    }

    [Fact]
    public async Task Topic_Of_Another_Semester_Is_Rejected()
    {
        var (group, targetClass, topic) = Setup(_summer);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            Service(group, targetClass, topic).SelectTopicAsync(group.Id, new SelectTopicRequest { TopicId = topic.Id }));
    }

    [Fact]
    public async Task Topic_Without_Semester_Is_Open_To_Every_Semester()
    {
        var (group, targetClass, topic) = Setup(null);

        await Service(group, targetClass, topic).SelectTopicAsync(group.Id, new SelectTopicRequest { TopicId = topic.Id });

        Assert.Equal(topic.Id, group.CourseTopicId);
    }
}
