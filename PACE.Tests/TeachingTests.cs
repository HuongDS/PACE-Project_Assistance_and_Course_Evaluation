using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using PACE.API.Controllers;
using PACE.Application.Common.Exceptions;
using PACE.Infrastructure.Identity;
using Xunit;
namespace PACE.Tests;
public class TeachingTests
{
    [Theory]
    [InlineData("CREATED", "CREATED")]
    [InlineData("IN_PROGRESS", "IN_PROGRESS")]
    public void ValidLifecycleState(string state, string required) => SessionRules.Require(state, required);
    [Theory]
    [InlineData("COMPLETED", "CREATED")]
    [InlineData("COMPLETED", "IN_PROGRESS")]
    [InlineData("CREATED", "IN_PROGRESS")]
    public void InvalidLifecycleState(string state, string required) => Assert.Throws<ConflictException>(() => SessionRules.Require(state, required));
    [Fact]
    public void ScoreMustBelongToRubricAndStayInRange()
    {
        var id = Guid.NewGuid(); var rubric = new List<SessionCriterion> { new(id, "Design", "", 10, 100) };
        SessionRules.ValidateScores(rubric, [new(id, 7.5m, null)]);
        Assert.Throws<BadRequestException>(() => SessionRules.ValidateScores(rubric, [new(id, 11, null)]));
        Assert.Throws<BadRequestException>(() => SessionRules.ValidateScores(rubric, [new(Guid.NewGuid(), 5, null)]));
        Assert.Throws<BadRequestException>(() => SessionRules.ValidateScores(rubric, []));
        Assert.Throws<BadRequestException>(() => SessionRules.ValidateScores(rubric, [new(id, 5, null), new(id, 5, null)]));
    }
    [Fact]
    public void PdfHasPagesAndValidCrossReferenceOffset()
    {
        var bytes = ReportPdf.Create(Enumerable.Range(0, 150).Select(i => $"Score ({i}) \\ saved")); var text = Encoding.ASCII.GetString(bytes);
        Assert.StartsWith("%PDF-1.4", text); Assert.Contains("/Count 4", text); Assert.EndsWith("%%EOF\n", text);
        var offset = int.Parse(text.Split("startxref\n")[1].Split('\n')[0]); Assert.StartsWith("xref\n", text[offset..]);
    }
    [Fact]
    public void AdminPasswordFailsClosedAndChecksPbkdf2()
    {
        var empty = new AdminCredentialValidator(new ConfigurationBuilder().Build()); Assert.False(empty.Validate("password"));
        var salt = RandomNumberGenerator.GetBytes(16); var hash = Rfc2898DeriveBytes.Pbkdf2("correct", salt, 210000, HashAlgorithmName.SHA256, 32);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "AdminLogin:PasswordHash", Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash) } }).Build();
        var validator = new AdminCredentialValidator(config); Assert.True(validator.Validate("correct")); Assert.False(validator.Validate("incorrect"));
    }

    [Fact]
    public void PdfPreservesVietnameseCharactersAndOffsets()
    {
        var lines = new List<string>
        {
            "PACE Báo cáo Đánh giá Khóa luận",
            "Sinh viên: Nguyễn Văn An - MSSV: SE170001",
            "Đề tài: Hệ thống Quản lý và Đánh giá Khóa học (PACE)",
            "Đánh giá: Sinh viên trả lời xuất sắc các câu hỏi kiến trúc và giải thuật.",
            "Điểm số đạt mức A (92/100). Chúc mừng sinh viên!"
        };

        var bytes = ReportPdf.Create(lines);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.StartsWith("%PDF-1.4", text);
        Assert.EndsWith("%%EOF\n", text);
        Assert.Contains("Nguyễn Văn An", text);
        Assert.Contains("Đề tài: Hệ thống Quản lý", text);
        Assert.DoesNotContain("?ề tài", text);

        var offset = int.Parse(text.Split("startxref\n")[1].Split('\n')[0]);
        var xrefSlice = Encoding.UTF8.GetString(bytes.AsSpan(offset));
        Assert.StartsWith("xref\n", xrefSlice);
    }

    [Theory]
    [InlineData(95, "A")]
    [InlineData(85, "A")]
    [InlineData(84.9, "B")]
    [InlineData(70, "B")]
    [InlineData(55, "C")]
    [InlineData(40, "D")]
    [InlineData(39.9, "F")]
    [InlineData(0, "F")]
    public void ComputeGradeLetterCalculatesCorrectGrades(decimal score, string expected)
    {
        Assert.Equal(expected, ReportsController.ComputeGradeLetter(score));
    }

    [Fact]
    public void ComputeGradeLetterNullReturnsNull()
    {
        Assert.Null(ReportsController.ComputeGradeLetter(null));
    }

    [Fact]
    public void OralExamAndAssessmentSessionRelationship()
    {
        var examId = Guid.NewGuid();
        var exam = new PACE.Domain.Entities.OralExam
        {
            Id = examId,
            Title = "Kiểm tra vấn đáp cuối kỳ",
            ClassId = Guid.NewGuid()
        };

        var session = new PACE.Domain.Entities.AssessmentSession
        {
            Id = Guid.NewGuid(),
            Title = "Ca thi nhóm 01",
            OralExamId = examId,
            OralExam = exam
        };
        exam.AssessmentSessions.Add(session);

        Assert.Equal(examId, session.OralExamId);
        Assert.Same(exam, session.OralExam);
        Assert.Contains(session, exam.AssessmentSessions);
    }
}
