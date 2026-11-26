using Microsoft.AspNetCore.Authorization;
using PACE.API.Common;
using PACE.API.Controllers;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using Xunit;

namespace PACE.Tests;

/// <summary>Syllabus nhập vào phải nhất quán, và đọc ra đúng như đã nhập.</summary>
public class CourseSyllabusTests
{
    private static SyllabusAssessment Column(string category, decimal weight) =>
        new(category, "on-going", 1, weight, "- Tài liệu (100%): đủ mục", null, ["CLO1"]);

    private static SaveCourseSyllabus Request(List<SyllabusAssessment>? columns = null, List<SyllabusSession>? sessions = null) =>
        new("1", "Môn thử",
            columns ?? [Column("Milestone 1", 40), Column("Final", 60)],
            sessions ?? [new(2, "Milestone 1 Review"), new(1, "Course Introduction")]);

    [Fact]
    public void Content_RoundTrips_WithSessionsInOrder()
    {
        var item = new CourseSyllabus { CourseId = Guid.NewGuid(), Name = "Môn thử", ContentJson = SyllabusRules.Serialize(Request()) };

        var view = SyllabusRules.View(item);

        Assert.Equal(["Milestone 1", "Final"], view.Assessments.Select(x => x.Category));
        Assert.Equal(["CLO1"], view.Assessments[0].Clos!);
        Assert.Equal([1, 2], view.Sessions.Select(x => x.Number));
    }

    [Fact]
    public void Weights_Above_100_Are_Rejected() =>
        Assert.Throws<BadRequestException>(() => SyllabusRules.Serialize(Request([Column("A", 70), Column("B", 40)])));

    [Fact]
    public void Repeated_Session_Number_Is_Rejected() =>
        Assert.Throws<BadRequestException>(() => SyllabusRules.Serialize(Request(sessions: [new(1, "A"), new(1, "B")])));

    [Fact]
    public void Blank_Topic_Is_Rejected() =>
        Assert.Throws<BadRequestException>(() => SyllabusRules.Serialize(Request(sessions: [new(1, " ")])));

    [Theory]
    [InlineData("Get", Roles.ClassStaff)]
    [InlineData("Save", Roles.Registrar)]
    public void Lecturer_Reads_But_Only_Registrar_Writes(string method, string expected)
    {
        var roles = typeof(CourseSyllabusController).GetMethod(method)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Roles;

        Assert.Equal(expected, roles);
        Assert.True(typeof(CourseSyllabusController).IsDefined(typeof(AllowTrainingOfficeAttribute), true));
    }
}
