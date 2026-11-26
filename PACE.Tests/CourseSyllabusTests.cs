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
        new(Guid.NewGuid(), "1", "Môn thử",
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

    [Fact]
    public void Class_Gets_The_Version_In_Force_In_Its_Semester()
    {
        CourseSyllabus From(int year, int month) => new() { Name = $"{year}-{month}", Semester = new Semester { StartDate = new DateOnly(year, month, 1) } };
        var versions = new[] { From(2026, 1), From(2026, 9) };

        Assert.Equal("2026-1", SyllabusRules.Effective(versions, new DateOnly(2026, 5, 4))!.Name);
        Assert.Equal("2026-9", SyllabusRules.Effective(versions, new DateOnly(2027, 1, 4))!.Name);
        Assert.Equal("2026-9", SyllabusRules.Effective(versions, null)!.Name);
        Assert.Null(SyllabusRules.Effective(versions, new DateOnly(2025, 9, 1)));
    }

    [Fact]
    public void Only_Unused_Course_Can_Be_Deleted()
    {
        CourseRules.RequireUnused(0, 0, 0);
        Assert.Throws<ConflictException>(() => CourseRules.RequireUnused(1, 0, 0));
        Assert.Throws<ConflictException>(() => CourseRules.RequireUnused(0, 2, 0));
        Assert.Throws<ConflictException>(() => CourseRules.RequireUnused(0, 0, 1));
    }

    [Theory]
    [InlineData("Get", Roles.ClassStaff)]
    [InlineData("GetVersion", Roles.ClassStaff)]
    [InlineData("Save", Roles.Registrar)]
    [InlineData("Delete", Roles.Registrar)]
    public void Lecturer_Reads_But_Only_Registrar_Writes(string method, string expected)
    {
        var roles = typeof(CourseSyllabusController).GetMethod(method)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Roles;

        Assert.Equal(expected, roles);
        Assert.True(typeof(CourseSyllabusController).IsDefined(typeof(AllowTrainingOfficeAttribute), true));
    }
}
