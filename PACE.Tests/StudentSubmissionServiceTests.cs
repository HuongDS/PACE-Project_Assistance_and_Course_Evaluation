using System.Reflection;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Domain.Entities;
using Xunit;

namespace PACE.Tests;

public class StudentSubmissionServiceTests
{
    private readonly User user = new() { Id = Guid.NewGuid(), StudentCode = "001ABC", Email = "student@gmail.com" };
    private readonly List<Business> businesses = [];
    private readonly List<StudentSubmission> saved = [];
    private readonly StudentSubmissionService service;
    private bool transaction;
    private bool locked;

    public StudentSubmissionServiceTests()
    {
        var run = new AnalysisRun { Id = Guid.NewGuid(), RepoName = "repo", RepoUrl = "https://example.com/repo" };
        user.AnalysisRun = run;
        user.AnalysisRunId = run.Id;
        for (var i = 0; i < 3; i++)
        {
            var b = new Business { Id = Guid.NewGuid(), AnalysisRunId = run.Id, BusinessName = $"Business {i}" };
            b.Questions.Add(new GeneratedQuestion { Id = Guid.NewGuid(), BusinessId = b.Id, Business = b, QuestionText = $"Question {i}" });
            businesses.Add(b);
        }
        var users = Stub<IUserRepository>.Create((m, a) => m.Name switch
        {
            "GetByEmailAsync" => Task.FromResult<User?>(string.Equals((string)a[0]!, user.Email, StringComparison.OrdinalIgnoreCase) ? user : null),
            _ => throw new NotSupportedException(m.Name)
        });
        var repo = Stub<IStudentSubmissionRepository>.Create((m, a) =>
        {
            switch (m.Name)
            {
                case "LockUserAsync": Assert.True(transaction); locked = true; return Task.CompletedTask;
                case "GetBusinessesWithQuestionsAsync": return Task.FromResult(businesses.Where(b => b.AnalysisRunId == (Guid)a[0]! && b.Questions.Count > 0).ToList());
                case "ExistsAsync": return Task.FromResult(saved.Any(s => s.UserId == (Guid)a[0]! && s.AnalysisRunId == (Guid)a[1]!));
                case "GetOwnedAsync": return Task.FromResult(saved.FirstOrDefault(s => s.Id == (Guid)a[0]! && s.UserId == (Guid)a[1]!));
                case "Add":
                    Assert.True(transaction && locked);
                    var submission = (StudentSubmission)a[0]!;
                    submission.AnalysisRun = run;
                    foreach (var answer in submission.Answers)
                        answer.Question = businesses.SelectMany(b => b.Questions).Single(q => q.Id == answer.QuestionId);
                    saved.Add(submission); return null;
                default: throw new NotSupportedException(m.Name);
            }
        });
        var uow = Stub<IUnitOfWork>.Create((m, a) => m.Name switch
        {
            "get_Users" => users,
            "get_StudentSubmissions" => repo,
            "ExecuteInTransactionAsync" => Execute((Func<CancellationToken, Task<StudentSubmissionCreatedDto>>)a[0]!, (CancellationToken)a[1]!),
            _ => throw new NotSupportedException(m.Name)
        });
        service = new StudentSubmissionService(uow);
    }

    private async Task<StudentSubmissionCreatedDto> Execute(Func<CancellationToken, Task<StudentSubmissionCreatedDto>> action, CancellationToken ct)
    {
        transaction = true;
        try { return await action(ct); }
        finally { transaction = false; }
    }
    private StudentIdentityRequest Identity() => new() { StudentCode = user.StudentCode!, Email = user.Email };
    private SubmitStudentSubmissionRequest Request() => new()
    {
        StudentCode = user.StudentCode!, Email = user.Email,
        Answers = businesses.Select(b => new StudentAnswerRequest { BusinessId = b.Id, QuestionId = b.Questions.First().Id, AnswerText = " answer " }).ToList()
    };

    [Fact] public async Task LookupReturnsOnlyBusinessesWithQuestionsAndNoSuggestedAnswers()
    {
        businesses.Add(new Business { Id = Guid.NewGuid(), AnalysisRunId = user.AnalysisRunId!.Value });
        var result = await service.LookupAsync(Identity());
        Assert.Equal(3, result.Businesses.Count);
        Assert.All(result.Businesses, b => Assert.Single(b.Questions));
        Assert.Equal(user.AnalysisRunId, result.Repository.AnalysisRunId);
    }
    [Theory] [InlineData("wrong", "student@gmail.com")] [InlineData("001ABC", "wrong@gmail.com")]
    public async Task WrongIdentityRejected(string code, string email)
        => await Assert.ThrowsAsync<NotFoundException>(() => service.LookupAsync(new() { StudentCode = code, Email = email }));
    [Fact] public async Task AdminIdentityRejected()
    {
        user.Role = PACE.Domain.Enums.UserRole.Admin;
        await Assert.ThrowsAsync<NotFoundException>(() => service.LookupAsync(Identity()));
    }
    [Fact] public async Task NoRepositoryRejected()
    {
        user.AnalysisRunId = null;
        await Assert.ThrowsAsync<UnprocessableException>(() => service.LookupAsync(Identity()));
        await Assert.ThrowsAsync<UnprocessableException>(() => service.SubmitAsync(Request()));
    }
    [Fact] public async Task SubmitSavesThreeAnswersAndRetrieveReturnsOwnedSubmission()
    {
        var result = await service.SubmitAsync(Request());
        Assert.Equal(3, result.AnswerCount);
        Assert.Equal(DateTimeKind.Utc, result.SubmittedAt.Kind);
        var submission = Assert.Single(saved);
        Assert.All(submission.Answers, a => { Assert.Equal(user.Id, a.UserId); Assert.Equal(result.SubmissionId, a.SubmissionId); Assert.Equal("answer", a.AnswerText); });
        var detail = await service.RetrieveAsync(new() { StudentCode = user.StudentCode!, Email = user.Email, SubmissionId = result.SubmissionId });
        Assert.Equal(3, detail.Answers.Count);
        Assert.Equal(result.SubmissionId, detail.SubmissionId);
    }
    [Fact] public async Task DuplicateSubmissionRejected()
    {
        await service.SubmitAsync(Request());
        await Assert.ThrowsAsync<ConflictException>(() => service.SubmitAsync(Request()));
        Assert.Single(saved);
    }
    [Theory] [InlineData(0)] [InlineData(2)] [InlineData(4)]
    public async Task WrongAnswerCountRejected(int count)
    {
        var request = Request(); request.Answers = Enumerable.Repeat(request.Answers[0], count).ToList();
        await Assert.ThrowsAsync<BadRequestException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Fact] public async Task DuplicateBusinessRejected()
    {
        var request = Request(); request.Answers[1].BusinessId = request.Answers[0].BusinessId;
        await Assert.ThrowsAsync<BadRequestException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Theory] [InlineData("")] [InlineData("  ")]
    public async Task BlankAnswerRejected(string text)
    {
        var request = Request(); request.Answers[0].AnswerText = text;
        await Assert.ThrowsAsync<BadRequestException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Fact] public async Task QuestionFromDifferentBusinessRejected()
    {
        var request = Request(); request.Answers[0].QuestionId = request.Answers[1].QuestionId;
        await Assert.ThrowsAsync<BadRequestException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Fact] public async Task BusinessFromDifferentRepositoryRejected()
    {
        var request = Request(); businesses[0].AnalysisRunId = Guid.NewGuid();
        var extra = new Business { Id = Guid.NewGuid(), AnalysisRunId = user.AnalysisRunId!.Value };
        extra.Questions.Add(new GeneratedQuestion { Id = Guid.NewGuid() }); businesses.Add(extra);
        await Assert.ThrowsAsync<BadRequestException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Fact] public async Task InsufficientBusinessesRejected()
    {
        var request = Request(); businesses[0].Questions.Clear();
        await Assert.ThrowsAsync<UnprocessableException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Fact] public async Task AnotherStudentsSubmissionIsHidden()
    {
        saved.Add(new StudentSubmission { Id = Guid.NewGuid(), UserId = Guid.NewGuid() });
        await Assert.ThrowsAsync<NotFoundException>(() => service.RetrieveAsync(new() { StudentCode = user.StudentCode!, Email = user.Email, SubmissionId = saved[0].Id }));
    }
    [Fact] public async Task RetrieveRejectsWrongIdentity()
        => await Assert.ThrowsAsync<NotFoundException>(() => service.RetrieveAsync(new() { StudentCode = "wrong", Email = user.Email, SubmissionId = Guid.NewGuid() }));
    [Fact] public async Task SubmitRejectsWrongStudentCode()
    {
        var request = Request(); request.StudentCode = "wrong";
        await Assert.ThrowsAsync<NotFoundException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Theory] [InlineData("", "student@gmail.com")] [InlineData("001ABC", "invalid-email")]
    public async Task MalformedIdentityRejected(string code, string email)
        => await Assert.ThrowsAsync<BadRequestException>(() => service.LookupAsync(new() { StudentCode = code, Email = email }));
    [Fact] public async Task NullAnswerRejected()
    {
        var request = Request(); request.Answers[0] = null!;
        await Assert.ThrowsAsync<BadRequestException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Fact] public async Task UnknownQuestionRejected()
    {
        var request = Request(); request.Answers[0].QuestionId = Guid.NewGuid();
        await Assert.ThrowsAsync<BadRequestException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Fact] public async Task RetrieveStillWorksAfterRepositoryUnassigned()
    {
        var created = await service.SubmitAsync(Request()); user.AnalysisRunId = null; user.AnalysisRun = null;
        var detail = await service.RetrieveAsync(new() { StudentCode = user.StudentCode!, Email = user.Email, SubmissionId = created.SubmissionId });
        Assert.Equal(3, detail.Answers.Count);
    }
    [Fact] public async Task EmptySubmissionIdRejected()
        => await Assert.ThrowsAsync<BadRequestException>(() => service.RetrieveAsync(new() { StudentCode = user.StudentCode!, Email = user.Email }));
}

public class Stub<T> : DispatchProxy where T : class
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Create(Func<MethodInfo, object?[], object?> handler)
    {
        var proxy = Create<T, Stub<T>>(); ((Stub<T>)(object)proxy).Handler = handler; return proxy;
    }
}
