using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Domain.Entities;
using Xunit;

namespace PACE.Tests;

public class SurveySubmissionTests
{
    [Fact]
    public async Task SurveyPersistsScoresAndRejectsResubmissionWithoutOverwriting()
    {
        var f = new Fixture();
        await f.Service.SubmitSurveyAsync(f.Request(4), f.Submission.UserId);
        Assert.Equal(4, Assert.Single(f.Answer.RubricScores).Score);
        Assert.Equal("Helpful", f.Answer.StudentReply);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.SubmitSurveyAsync(f.Request(2), f.Submission.UserId));
        Assert.Equal(4, Assert.Single(f.Answer.RubricScores).Score);
        Assert.Equal(1, f.Insertions);
        Assert.Equal(1, f.Saves);
    }

    [Fact]
    public async Task InvalidCriterionDoesNotSilentlySucceedOrPartiallyChangeReply()
    {
        var f = new Fixture();
        var request = f.Request(4);
        request.Answers[0].Scores.Add(new(Guid.NewGuid(), 5));
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.SubmitSurveyAsync(request, f.Submission.UserId));
        Assert.Empty(f.Answer.RubricScores);
        Assert.Null(f.Answer.StudentReply);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task AnotherStudentCannotSubmitRatings()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SubmitSurveyAsync(f.Request(4), Guid.NewGuid()));
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task UnknownAnswerCannotBeSilentlySkipped()
    {
        var f = new Fixture();
        var request = f.Request(4);
        request.Answers[0].StudentAnswerId = Guid.NewGuid();
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.SubmitSurveyAsync(request, f.Submission.UserId));
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task IncompleteSurveyDoesNotLockOrSavePartialRatings()
    {
        var f = new Fixture();
        f.Submission.Answers.Add(new StudentAnswer { Id = Guid.NewGuid(), QuestionId = Guid.NewGuid() });
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.SubmitSurveyAsync(f.Request(4), f.Submission.UserId));
        Assert.Empty(f.Answer.RubricScores);
        Assert.Null(f.Answer.StudentReply);
        Assert.Equal(0, f.Saves);
    }

    private sealed class Fixture
    {
        public StudentAnswer Answer = new() { Id = Guid.NewGuid(), QuestionId = Guid.NewGuid() };
        public StudentSubmission Submission;
        public RubricCriterion Criterion = new() { Id = Guid.NewGuid() };
        public RubricCriterionService Service;
        public int Saves, Insertions;
        public Fixture()
        {
            Submission = new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), SubmittedAt = DateTime.UtcNow, Answers = [Answer] };
            var repo = Stub<IStudentSubmissionRepository>.Create((m, a) => m.Name switch
            {
                "LockUserAsync" => Task.CompletedTask,
                "GetForSurveyUpdateAsync" => Task.FromResult<StudentSubmission?>(Submission),
                "AddSurveyScore" => Add((RubricScore)a[0]!),
                _ => throw new InvalidOperationException(m.Name)
            });
            var criteria = Stub<IRubricCriterionRepository>.Create((m, a) =>
                Task.FromResult(new List<RubricCriterion> { Criterion }));
            var uow = Stub<IUnitOfWork>.Create((m, a) => m.Name switch
            {
                "get_StudentSubmissions" => repo,
                "get_RubricCriteria" => criteria,
                "ExecuteInTransactionAsync" => ((Func<CancellationToken, Task>)a[0]!)(default),
                "SaveChangesAsync" => Save(),
                _ => throw new InvalidOperationException(m.Name)
            });
            Service = new(uow, Stub<ISurveyExcelService>.Create((m,a) => throw new NotImplementedException()));
        }
        private object? Add(RubricScore score) { Insertions++; Answer.RubricScores.Add(score); return null; }
        private Task<int> Save() { Saves++; return Task.FromResult(1); }
        public SubmitSurveyRequest Request(int score) => new() { SubmissionId = Submission.Id,
            Answers = [new() { StudentAnswerId = Answer.Id, QuestionId = Answer.QuestionId,
                StudentReply = " Helpful ", Scores = [new(Criterion.Id, score)] }] };
    }
}
