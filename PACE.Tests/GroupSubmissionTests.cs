using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using PACE.API.Controllers;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using Xunit;

namespace PACE.Tests;

/// <summary>Bài nhóm nộp phải là tệp hoặc liên kết an toàn, và đánh giá của giảng viên phải đủ ý.</summary>
public class GroupSubmissionTests
{
    [Theory]
    [InlineData("SRS.pdf", "application/pdf")]
    [InlineData("erd.PNG", "image/png")]
    [InlineData("thu muc\\bao-cao.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void Accepted_File_Gets_The_Type_From_Its_Extension(string name, string type) =>
        Assert.Equal(type, SubmissionRules.File(name, 10).ContentType);

    [Fact]
    public void Path_Is_Removed_From_The_File_Name() =>
        Assert.Equal("bao-cao.pdf", SubmissionRules.File("../../bao-cao.pdf", 10).Name);

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("page.html")]
    [InlineData("logo.svg")]
    [InlineData("script.js")]
    [InlineData("khong-duoi")]
    public void Executable_Or_Active_Content_Is_Rejected(string name) =>
        Assert.Throws<BadRequestException>(() => SubmissionRules.File(name, 10));

    [Theory]
    [InlineData(0)]
    [InlineData(SubmissionRules.FileLimit + 1)]
    public void Empty_Or_Oversized_File_Is_Rejected(long size) =>
        Assert.Throws<BadRequestException>(() => SubmissionRules.File("a.pdf", size));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("github.com/pace")]
    public void Only_Web_Links_Are_Accepted(string url) =>
        Assert.Throws<BadRequestException>(() => SubmissionRules.Link(url));

    [Fact]
    public void Web_Link_Is_Trimmed() =>
        Assert.Equal("https://github.com/pace/demo", SubmissionRules.Link("  https://github.com/pace/demo "));

    [Fact]
    public void Returning_Work_Needs_A_Reason()
    {
        Assert.Throws<BadRequestException>(() => SubmissionRules.Review(new("NeedsRevision", null, " ")));
        Assert.Equal(SubmissionReviewStatus.NeedsRevision, SubmissionRules.Review(new("NeedsRevision", null, "Thiếu ERD.")));
        Assert.Equal(SubmissionReviewStatus.Accepted, SubmissionRules.Review(new("Accepted", 8.5m, null)));
    }

    [Theory]
    [InlineData("Accepted", 10.5)]
    [InlineData("Accepted", -1)]
    [InlineData("Accepted", 7.125)]
    [InlineData("Maybe", 7)]
    public void Invalid_Review_Is_Rejected(string status, double score) =>
        Assert.Throws<BadRequestException>(() => SubmissionRules.Review(new(status, (decimal)score, null)));

    [Fact]
    public void Required_Deliverables_Come_From_The_Latest_Revision_Of_Each_Round()
    {
        static string Config(params string[] deliverables) =>
            JsonSerializer.Serialize(new AssessmentRoundConfiguration([.. deliverables], [], null, [], [], null));
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var required = SubmissionRules.Required(
        [
            (first, 1, Config("SRS", "Bản nháp")),
            (first, 2, Config("SRS", "ERD")),
            (second, 1, Config("erd", "Test report")),
        ]);

        Assert.Equal(["SRS", "ERD", "Test report"], required);
    }

    [Theory]
    [InlineData("Submit", "Student")]
    [InlineData("Delete", "Student")]
    [InlineData("Review", "Admin,Lecturer")]
    public void Write_Endpoints_Are_Limited_By_Role(string method, string roles) =>
        Assert.Contains(typeof(GroupSubmissionsController).GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>(), x => x.Roles == roles);
}
