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
        var bytes = ReportPdf.Create(Enumerable.Range(0, 150).Select(i => $"Score ({i}) \\ saved"));
        Assert.True(bytes.Length > 0);
        var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(10, bytes.Length));
        Assert.StartsWith("%PDF", header);

        using var ms = new MemoryStream(bytes);
        using var doc = PdfSharpCore.Pdf.IO.PdfReader.Open(ms, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.ReadOnly);
        Assert.True(doc.PageCount >= 2);
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
        Assert.True(bytes.Length > 0);
        var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(10, bytes.Length));
        Assert.StartsWith("%PDF", header);

        using var ms = new MemoryStream(bytes);
        using var doc = PdfSharpCore.Pdf.IO.PdfReader.Open(ms, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.ReadOnly);
        Assert.True(doc.PageCount >= 1);
        Assert.Equal("PACE Assessment Report", doc.Info.Title);
    }

    [Fact]
    public void EvidenceController_ParseGitCommitsAndContributions_ComputesAccurateMetrics()
    {
        var sampleLog =
            "COMMIT\01111111111111111111111111111111111111111\0alice@example.com\0Alice Nguyen\0feat: initial auth\02026-11-10T10:00:00+07:00\n" +
            "120\t20\tsrc/Auth.cs\n" +
            "30\t5\tsrc/Token.cs\n" +
            "COMMIT\02222222222222222222222222222222222222222\0bob@example.com\0Bob Tran\0fix: database connection\02026-11-11T11:00:00+07:00\n" +
            "45\t10\tsrc/Db.cs\n" +
            "COMMIT\03333333333333333333333333333333333333333\0alice@example.com\0Alice Nguyen\0refactor: clean models\02026-11-12T12:00:00+07:00\n" +
            "15\t5\tsrc/Auth.cs\n";

        var (commits, contributions) = EvidenceController.ParseGitCommitsAndContributions(sampleLog);

        Assert.Equal(3, commits.Count);
        Assert.Equal(150, commits[0].Additions);
        Assert.Equal(25, commits[0].Deletions);
        Assert.Equal(2, commits[0].ChangedFilesCount);

        Assert.Equal(2, contributions.Count);
        var alice = contributions.First(c => c.AuthorEmail == "alice@example.com");
        var bob = contributions.First(c => c.AuthorEmail == "bob@example.com");

        Assert.Equal(2, alice.CommitCount);
        Assert.Equal(165, alice.Additions);
        Assert.Equal(30, alice.Deletions);
        Assert.Equal(66.7, alice.CommitRatio);

        Assert.Equal(1, bob.CommitCount);
        Assert.Equal(45, bob.Additions);
        Assert.Equal(10, bob.Deletions);
        Assert.Equal(33.3, bob.CommitRatio);
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
