using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Domain.Entities;
using Xunit;

namespace PACE.Tests;

public class AdminSurveyTests
{
    private static readonly Guid Criterion = Guid.NewGuid();
    private static SurveySubmissionItemDto Row(int score = 4) => new(Guid.NewGuid(), Guid.NewGuid(), "student@gmail.com", "SE192251", "Repo A",
        "Chat", "Explain validation", "Checks input", "{\"criteria\":[{\"name\":\"Correctness\",\"score\":8},{\"name\":\"Clarity\",\"score\":2}]}",
        "Useful feedback", score, new DateTime(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc),
        [new(Guid.NewGuid(), Criterion, "ACCURACY", "Accuracy", "Accurate", score)])
        { AnalysisRunId = Guid.NewGuid(), QuestionId = Guid.NewGuid(), BusinessId = Guid.NewGuid(), FeedbackFormat = "Rubric" };

    [Fact] public void CombinedFiltersMatchAndWrongRepositoryDoesNot()
    {
        var row = Row();
        var query = new SurveyResultQuery {AnalysisRunId = row.AnalysisRunId, QuestionId = row.QuestionId, BusinessId = row.BusinessId,
            StudentCode = " se192 ", StudentEmail = "GMAIL", RepoName = "repo a", BusinessName = "chat", QuestionText = "validation",
            AnswerText = "input", AiFeedback = "Correctness", StudentReply = "useful", FeedbackFormat = "rubric",
            CriterionId = Criterion, MinScore = 4, MaxScore = 5, MinAverageScore = 4, HasSurvey = true, HasFeedback = true};
        Assert.True(SurveyResultFiltering.Matches(row, query));
        query.AnalysisRunId = Guid.NewGuid();
        Assert.False(SurveyResultFiltering.Matches(row, query));
    }
    [Fact] public void AiNameAndScoreMustMatchTheSameCriterion()
    {
        Assert.True(SurveyResultFiltering.Matches(Row(), new() {AiCriterionName = "correct", MinAiScore = 7}));
        Assert.False(SurveyResultFiltering.Matches(Row(), new() {AiCriterionName = "clarity", MinAiScore = 7}));
        Assert.False(SurveyResultFiltering.Matches(Row() with {AiFeedback = "legacy plain text"}, new() {MinAiScore = 1}));
    }
    [Fact] public void SurveyScoreRangeMustMatchOneScoreNotTwoDifferentScores()
    {
        var row = Row() with {Scores = [new(Guid.NewGuid(), Criterion, "A", "A", "A", 1), new(Guid.NewGuid(), Guid.NewGuid(), "B", "B", "B", 5)]};
        Assert.False(SurveyResultFiltering.Matches(row, new() {MinScore = 2, MaxScore = 4}));
    }
    [Fact] public void PendingSurveyIsNotRatedZeroAndK6CanBeIncludedExplicitly()
    {
        var row = Row() with {Scores = [], AverageScore = 0};
        Assert.True(SurveyResultFiltering.Matches(row, new() {HasSurvey = false}));
        Assert.False(SurveyResultFiltering.Matches(row, new() {MinAverageScore = 1}));
        row = Row() with {StudentCode = "K6BATCHX0001"};
        Assert.False(SurveyResultFiltering.Matches(row, new()));
        Assert.True(SurveyResultFiltering.Matches(row, new() {ExcludeTestData = false}));
    }
    [Fact] public void DateFiltersHonorTimezoneOffsetsAndInclusiveBoundaries()
    {
        var row = Row();
        Assert.True(SurveyResultFiltering.Matches(row, new() {FromDate = DateTimeOffset.Parse("2026-10-20T10:00:00+07:00"), ToDate = DateTimeOffset.Parse("2026-10-20T10:00:00+07:00")}));
        Assert.False(SurveyResultFiltering.Matches(row, new() {FromDate = DateTimeOffset.Parse("2026-10-20T10:01:00+07:00")}));
    }
    [Fact] public void ReversedRangesAreRejected()
        => Assert.Throws<BadRequestException>(() => SurveyResultFiltering.Validate(new() {MinScore = 5, MaxScore = 1}));

    [Fact] public void ExcelContainsTraceableIdsAndDoesNotTurnMissingSurveyIntoZero()
    {
        var row = Row() with {Scores = [], AverageScore = 0};
        var summary = new SurveySummaryResponseDto(1, 1, 0, 0, "", "", [], [row]);
        var bytes = new PACE.Infrastructure.Excel.ClosedXmlSurveyExcelService().ExportSurveyResults(summary);
        using var stream = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        Assert.Equal("—", workbook.Worksheet("Tổng quan thống kê").Cell(3, 5).GetString());
        var sheet = workbook.Worksheet("Chi tiết khảo sát sinh viên");
        var headers = sheet.Row(1).CellsUsed().ToDictionary(c => c.GetString(), c => c.Address.ColumnNumber);
        Assert.Equal(row.QuestionId.ToString(), sheet.Cell(2, headers["Question ID"]).GetString());
        Assert.Equal(row.AnalysisRunId.ToString(), sheet.Cell(2, headers["Repository ID"]).GetString());
        Assert.Equal("Rubric", sheet.Cell(2, headers["Loại feedback"]).GetString());
        Assert.Equal("—", sheet.Cell(2, headers["Điểm TB câu"]).GetString());
    }

    [Fact] public async Task SummaryIncludesMoreThanTwoHundredSubmissionsAndExportUsesIdenticalFilters()
    {
        var criterion = new RubricCriterion {Id = Criterion, Code = "A", Name = "Accuracy", Statement = "Accurate"};
        var run = new AnalysisRun {Id = Guid.NewGuid(), RepoName = "Repository"};
        var business = new Business {Id = Guid.NewGuid(), AnalysisRunId = run.Id, BusinessName = "Chat"};
        var question = new GeneratedQuestion {Id = Guid.NewGuid(), BusinessId = business.Id, Business = business, QuestionText = "Question"};
        var user = new User {Id = Guid.NewGuid(), Email = "student@example.com", StudentCode = "SE100"};
        var rows = Enumerable.Range(0, 250).Select(i => {
            var submission = new StudentSubmission {Id = Guid.NewGuid(), AnalysisRunId = run.Id, AnalysisRun = run, SubmittedAt = DateTime.UtcNow};
            var answer = new StudentAnswer {Id = Guid.NewGuid(), Submission = submission, User = user, QuestionId = question.Id, Question = question};
            answer.RubricScores.Add(new() {Id = Guid.NewGuid(), CriterionId = Criterion, Criterion = criterion, Score = i == 0 ? 1 : 5});
            return answer;
        }).ToList();
        var repo = Stub<IStudentSubmissionRepository>.Create((m,a) => m.Name == "GetSurveyAnswersAsync" ? Task.FromResult(rows) : throw new NotSupportedException(m.Name));
        var criteria = Stub<IRubricCriterionRepository>.Create((m,a) => Task.FromResult(new List<RubricCriterion> {criterion}));
        var uow = Stub<IUnitOfWork>.Create((m,a) => m.Name switch {"get_StudentSubmissions" => repo, "get_RubricCriteria" => criteria, _ => throw new NotSupportedException(m.Name)});
        SurveySummaryResponseDto? exported = null;
        var excel = Stub<ISurveyExcelService>.Create((m,a) => {exported = (SurveySummaryResponseDto)a[0]!;return new byte[] {1};});
        var service = new RubricCriterionService(uow, excel);
        var all = await service.GetSurveyResultsAsync();
        Assert.Equal(250, all.TotalSubmissions);
        var query = new SurveyResultQuery {CriterionId = Criterion, MinScore = 1, MaxScore = 1, QuestionId = question.Id};
        var filtered = await service.GetSurveyResultsAsync(query);
        Assert.Single(filtered.Submissions);
        Assert.Equal(1, filtered.TotalRatings);
        Assert.Equal(1, filtered.OverallAverageScore);
        await service.ExportSurveyResultsToExcelAsync(query);
        Assert.Equal(filtered.Submissions.Select(x => x.StudentAnswerId), exported!.Submissions.Select(x => x.StudentAnswerId));
    }
}
