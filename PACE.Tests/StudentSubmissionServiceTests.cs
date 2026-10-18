using PACE.Application.Dtos.StudentAnswer;
using PACE.Application.Services.Interfaces;
using System.Reflection;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using PACE.API.Controllers;
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
    private bool authenticated = true;
    private bool userExists = true;
    private Guid? claimedId;
    private UserRole claimedRole = UserRole.Student;
    private readonly List<FeedbackFormat> evaluatedFormats = [];

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
            "GetWithRepoAsync" => Task.FromResult<User?>(userExists && (Guid)a[0]! == user.Id ? user : null),
            _ => throw new NotSupportedException(m.Name)
        });
        var repo = Stub<IStudentSubmissionRepository>.Create((m, a) =>
        {
            switch (m.Name)
            {
                case "LockUserAsync": Assert.True(transaction); locked = true; return Task.CompletedTask;
                case "GetBusinessesWithQuestionsAsync": return Task.FromResult(businesses.Where(b => b.AnalysisRunId == (Guid)a[0]! && b.Questions.Count > 0).ToList());
                case "ExistsAsync": return Task.FromResult(saved.Any(s => s.UserId == (Guid)a[0]! && s.AnalysisRunId == (Guid)a[1]! && s.SubmittedAt != null));
                case "GetActiveSessionAsync": return Task.FromResult(saved.FirstOrDefault(s => s.UserId == (Guid)a[0]! && s.AnalysisRunId == (Guid)a[1]! && s.SubmittedAt == null));
                case "GetSubmittedAsync": return Task.FromResult(saved.FirstOrDefault(s => s.UserId == (Guid)a[0]! && s.AnalysisRunId == (Guid)a[1]! && s.SubmittedAt != null));
                case "GetByIdAsync": return Task.FromResult(saved.FirstOrDefault(s => s.Id == (Guid)a[0]!));
                case "GetOwnedAsync": return Task.FromResult(saved.FirstOrDefault(s => s.Id == (Guid)a[0]! && s.UserId == (Guid)a[1]!));
                case "GetPagedAsync":
                    var query = (StudentSubmissionQuery)a[0]!;
                    var items = saved.Where(x => !query.UserId.HasValue || x.UserId == query.UserId).ToList();
                    return Task.FromResult(new PagedResult<StudentSubmission> { Items = items, TotalCount = items.Count, Page = 1, PageSize = 20 });
                case "Update":
                    return null;
                case "Add":
                    Assert.True(transaction && locked);
                    var submission = (StudentSubmission)a[0]!;
                    submission.AnalysisRun = run;
                    submission.User = user;
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
            "SaveChangesAsync" => Task.FromResult(1),
            "ExecuteInTransactionAsync" when a[0] is Func<CancellationToken, Task<StudentSubmissionJoinedDto>> join => ExecuteJoin(join, (CancellationToken)a[1]!),
            "ExecuteInTransactionAsync" => Execute((Func<CancellationToken, Task<(StudentSubmission, List<Business>)>>)a[0]!, (CancellationToken)a[1]!),
            _ => throw new NotSupportedException(m.Name)
        });
        var current = Stub<ICurrentUserService>.Create((m, _) => m.Name switch
        {
            "get_IsAuthenticated" => authenticated,
            "get_UserId" => claimedId ?? user.Id,
            "get_Role" => claimedRole,
            _ => throw new NotSupportedException(m.Name)
        });
        var ansService = Stub<IStudentAnswerService>.Create((m, a) =>
        {
            if (m.Name == "EvaluateStudentAnswerAsync" && a[0] is StudentAnswerReq req)
                evaluatedFormats.Add(req.FeedbackFormat);
            return Task.FromResult("{}");
        });
        service = new StudentSubmissionService(uow, current, ansService);
    }

    private async Task<(StudentSubmission, List<Business>)> Execute(Func<CancellationToken, Task<(StudentSubmission, List<Business>)>> action, CancellationToken ct)
    {
        transaction = true;
        try { return await action(ct); }
        finally { transaction = false; }
    }
    private async Task<StudentSubmissionJoinedDto> ExecuteJoin(Func<CancellationToken, Task<StudentSubmissionJoinedDto>> action, CancellationToken ct)
    {
        transaction = true;
        try { return await action(ct); }
        finally { transaction = false; }
    }

    [Fact]
    public async Task JoinWithDifferentEmailUsesAuthenticatedStudentCodeAndResumesSameSession()
    {
        var request = new JoinStudentSubmissionRequest { Email = " collected@gmail.com ", StudentCode = " 001abc " };
        var result = await service.JoinAsync(request);
        Assert.Equal("collected@gmail.com", result.Email);
        Assert.Equal(user.StudentCode, result.StudentCode);
        Assert.Equal(user.Id, Assert.Single(saved).UserId);
        var resumed = await service.JoinAsync(request);
        Assert.Equal(result.SubmissionId, resumed.SubmissionId);
        Assert.Equal("collected@gmail.com", resumed.Email);
        Assert.Single(saved);
    }

    [Theory]
    [InlineData("OTHER-STUDENT")]
    [InlineData("")]
    public async Task JoinRejectsDifferentOrMissingStudentCodeEvenWithMatchingEmail(string code)
    {
        await Assert.ThrowsAsync<BadRequestException>(() => service.JoinAsync(new()
        {
            Email = user.Email, StudentCode = code
        }));
        Assert.Empty(saved);
    }

    [Fact]
    public async Task JoinStillRequiresEmail()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => service.JoinAsync(new()
        {
            Email = " ", StudentCode = user.StudentCode!
        }));
        Assert.Empty(saved);
    }

    private SubmitStudentSubmissionRequest Request() => new()
    {
        Answers = businesses.Select(b => new StudentAnswerRequest { BusinessId = b.Id, QuestionId = b.Questions.First().Id, AnswerText = " answer " }).ToList()
    };

    [Fact] public async Task LookupReturnsOnlyBusinessesWithQuestionsAndNoSuggestedAnswers()
    {
        businesses.Add(new Business { Id = Guid.NewGuid(), AnalysisRunId = user.AnalysisRunId!.Value });
        var result = await service.LookupAsync();
        Assert.Equal(3, result.Businesses.Count);
        Assert.All(result.Businesses, b => Assert.Single(b.Questions));
        Assert.Equal(user.AnalysisRunId, result.Repository.AnalysisRunId);
    }
    [Fact] public async Task UnknownUuidRejected()
    {
        claimedId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LookupAsync());
    }
    [Fact] public async Task AdminIdentityRejected()
    {
        user.Role = PACE.Domain.Enums.UserRole.Admin;
        await Assert.ThrowsAsync<ForbiddenException>(() => service.LookupAsync());
    }
    [Fact] public async Task NoRepositoryRejected()
    {
        user.AnalysisRunId = null;
        await Assert.ThrowsAsync<UnprocessableException>(() => service.LookupAsync());
        await Assert.ThrowsAsync<UnprocessableException>(() => service.SubmitAsync(Request()));
    }
    [Fact] public async Task SubmitSavesThreeAnswersAndRetrieveReturnsOwnedSubmission()
    {
        var result = await service.SubmitAsync(Request());
        Assert.Equal(3, result.EvaluatedAnswers.Count);
        Assert.NotNull(result.SubmittedAt);
        Assert.Equal(DateTimeKind.Utc, result.SubmittedAt!.Value.Kind);
        var submission = Assert.Single(saved);
        Assert.All(submission.Answers, a => { Assert.Equal(user.Id, a.UserId); Assert.Equal(result.SubmissionId, a.SubmissionId); Assert.Equal("answer", a.AnswerText); });
        var detail = await service.RetrieveAsync(new() { SubmissionId = result.SubmissionId });
        Assert.Equal(3, detail.Answers.Count);
        Assert.Equal(result.SubmissionId, detail.SubmissionId);
        Assert.Equal(3, evaluatedFormats.Count);
        Assert.Contains(FeedbackFormat.Sandwich, evaluatedFormats);
        Assert.Contains(FeedbackFormat.Rubric, evaluatedFormats);
        Assert.Contains(FeedbackFormat.Explainable, evaluatedFormats);
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
        await Assert.ThrowsAsync<BadRequestException>(() => service.SubmitAsync(request)); Assert.Empty(saved);
    }
    [Fact] public async Task AnotherStudentsSubmissionIsHidden()
    {
        saved.Add(new StudentSubmission { Id = Guid.NewGuid(), UserId = Guid.NewGuid() });
        await Assert.ThrowsAsync<NotFoundException>(() => service.RetrieveAsync(new() { SubmissionId = saved[0].Id }));
    }
    [Fact] public async Task UnauthenticatedRequestsRejected()
    {
        authenticated = false;
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LookupAsync());
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.SubmitAsync(Request()));
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.RetrieveAsync(new() { SubmissionId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.GetAllAsync(new()));
        Assert.Empty(saved);
    }

    [Fact] public async Task EmptyUuidRejected()
    {
        claimedId = Guid.Empty;
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LookupAsync());
    }

    [Fact] public async Task DeletedUserRejected()
    {
        userExists = false;
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LookupAsync());
    }

    [Fact] public async Task AdminJwtCannotUseStudentEndpoints()
    {
        claimedRole = UserRole.Admin;
        await Assert.ThrowsAsync<ForbiddenException>(() => service.LookupAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => service.SubmitAsync(Request()));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RetrieveAsync(new() { SubmissionId = Guid.NewGuid() }));
    }

    [Fact] public async Task StudentCannotListAllSubmissions()
        => await Assert.ThrowsAsync<ForbiddenException>(() => service.GetAllAsync(new()));

    [Fact] public async Task AdminCanListSubmissionsWithStudentAndAnswerDetails()
    {
        var submitted = await service.SubmitAsync(Request());
        claimedRole = UserRole.Admin;
        var result = await service.GetAllAsync(new() { UserId = user.Id });
        var item = Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(user.Id, item.UserId);
        Assert.Equal(user.StudentCode, item.StudentId);
        Assert.Equal(user.Email, item.Email);
        Assert.Equal(submitted.SubmissionId, item.Submission.SubmissionId);
        Assert.Equal(3, item.Submission.Answers.Count);
        Assert.Empty((await service.GetAllAsync(new() { UserId = Guid.NewGuid() })).Items);
    }

    [Fact] public async Task StudentHistoryCannotBeRedirectedToAnotherUser()
    {
        var submitted = await service.SubmitAsync(Request());
        var result = await service.GetMineAsync(new() { UserId = Guid.NewGuid() });
        Assert.Equal(submitted.SubmissionId, Assert.Single(result.Items).SubmissionId);
        claimedId = Guid.NewGuid();
        Assert.Empty((await service.GetMineAsync(new() { UserId = user.Id })).Items);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(submitted.SubmissionId));
    }

    [Fact] public async Task AdminCanReadFullSubmissionById()
    {
        var submitted = await service.SubmitAsync(Request());
        var answer = saved.Single().Answers.First();
        answer.AiFeedback = "{\"comment\":\"Good\"}";
        answer.FeedbackFormat = "Explainable";
        answer.StudentReply = "Thanks";
        answer.Question.SuggestedAnswer = "Reference answer";
        answer.Question.Difficulty = "Hard";
        answer.RubricScores.Add(new RubricScore
        {
            Id = Guid.NewGuid(), CriterionId = Guid.NewGuid(), Score = 4,
            Criterion = new RubricCriterion { Code = "C1", Name = "Logic", Statement = "Explain logic" }
        });
        claimedRole = UserRole.Admin;
        claimedId = Guid.NewGuid();
        var detail = await service.GetByIdAsync(submitted.SubmissionId);
        Assert.Equal(user.Email, detail.Student.Email);
        var mapped = detail.Answers.Single(a => a.StudentAnswerId == answer.Id);
        Assert.Equal(answer.AiFeedback, mapped.AiFeedback);
        Assert.Equal("Reference answer", mapped.SuggestedAnswer);
        Assert.Equal("Hard", mapped.Difficulty);
        Assert.Equal("Explainable", mapped.FeedbackFormat);
        Assert.Equal("Thanks", mapped.StudentReply);
        Assert.Equal(4, Assert.Single(mapped.RubricScores).Score);
    }

    [Fact] public async Task SubmissionReadsRequireAuthenticationAndSupportedRole()
    {
        authenticated = false;
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.GetByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.GetMineAsync(new()));
        authenticated = true;
        claimedRole = UserRole.Lecturer;
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetMineAsync(new()));
        claimedRole = UserRole.Admin;
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Lookup", "Student")]
    [InlineData("Submit", "Student")]
    [InlineData("Retrieve", "Student")]
    [InlineData("GetAll", "Admin")]
    public void ControllerRequiresCorrectRole(string method, string role)
    {
        var controller = typeof(StudentSubmissionsController);
        Assert.Empty(controller.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        var action = controller.GetMethod(method)!;
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Equal(role, action.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

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
        var detail = await service.RetrieveAsync(new() { SubmissionId = created.SubmissionId });
        Assert.Equal(3, detail.Answers.Count);
    }
    [Fact] public async Task EmptySubmissionIdRejected()
        => await Assert.ThrowsAsync<BadRequestException>(() => service.RetrieveAsync(new()));
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
