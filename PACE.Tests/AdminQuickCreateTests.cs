using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using Xunit;

namespace PACE.Tests;

public class AdminQuickCreateTests
{
    private readonly AnalysisRun run = new() {Id = Guid.NewGuid(), RepoName = "Ready repo", RepoUrl = "https://example.com/repo"};
    private User? user;
    private readonly List<StudentSubmission> submissions = [];
    private bool hasQuestions = true, collision, codeLocked, userLocked, transaction;
    private readonly StudentSubmissionService service;

    public AdminQuickCreateTests()
    {
        var users = Stub<IUserRepository>.Create((m,a) => m.Name switch {
            "FindAsync" => Task.FromResult(user == null ? new List<User>() : new List<User> {user}),
            "GetWithRepoAsync" => Task.FromResult(user),
            "EmailExistsAsync" => Task.FromResult(collision),
            "AddAsync" => AddUser((User)a[0]!),
            _ => throw new NotSupportedException(m.Name)
        });
        var runs = Stub<IAnalysisRunRepository>.Create((m,a) => Task.FromResult<AnalysisRun?>(run));
        var repo = Stub<IStudentSubmissionRepository>.Create((m,a) => {
            switch (m.Name) {
                case "GetBusinessesWithQuestionsAsync": return Task.FromResult(hasQuestions ? new List<Business> {new() {Id = Guid.NewGuid()}} : []);
                case "LockStudentCodeAsync": Assert.True(transaction);codeLocked = true;return Task.CompletedTask;
                case "LockUserAsync": Assert.True(transaction && codeLocked);userLocked = true;return Task.CompletedTask;
                case "GetSubmittedAsync": return Task.FromResult(submissions.FirstOrDefault(s => s.SubmittedAt != null));
                case "GetActiveSessionAsync": return Task.FromResult(submissions.FirstOrDefault(s => s.SubmittedAt == null));
                case "Add": Assert.True(transaction && codeLocked);submissions.Add((StudentSubmission)a[0]!);return null;
                case "GetByIdAsync": return Task.FromResult(submissions.FirstOrDefault(s => s.Id == (Guid)a[0]!));
                default: throw new NotSupportedException(m.Name);
            }
        });
        var uow = Stub<IUnitOfWork>.Create((m,a) => m.Name switch {
            "get_Users" => users, "get_AnalysisRuns" => runs, "get_StudentSubmissions" => repo,
            "SaveChangesAsync" => Task.FromResult(1),
            "ExecuteInTransactionAsync" => Execute((Func<CancellationToken,Task<AdminStudentSubmissionDto>>)a[0]!),
            _ => throw new NotSupportedException(m.Name)
        });
        var current = Stub<ICurrentUserService>.Create((m,a) => m.Name switch {
            "get_IsAuthenticated" => true, "get_UserId" => Guid.NewGuid(), "get_Role" => UserRole.Admin,
            _ => throw new NotSupportedException(m.Name)
        });
        var ai = Stub<IStudentAnswerService>.Create((m,a) => throw new Exception("Quick create must never invoke AI"));
        service = new(uow, current, ai);
    }
    private Task AddUser(User value) {Assert.True(codeLocked);user = value;return Task.CompletedTask;}
    private async Task<AdminStudentSubmissionDto> Execute(Func<CancellationToken,Task<AdminStudentSubmissionDto>> action)
    { transaction = true;try {return await action(default);}finally {transaction = false;} }
    private QuickCreateStudentSubmissionRequest Request() => new() {StudentCode = " se192251 ", Email = " STUDENT@GMAIL.COM ", AnalysisRunId = run.Id};
    [Fact] public async Task WalkInStudentCanLogInWithoutSheetAndRepeatedCreateReusesDraft()
    {
        var first = await service.QuickCreateAsync(Request());
        var second = await service.QuickCreateAsync(Request());
        Assert.Equal("SE192251", user!.StudentCode);Assert.Equal("student@gmail.com", user.Email);
        Assert.Equal(run.Id, user.AnalysisRunId);Assert.Equal(first.Submission.SubmissionId, second.Submission.SubmissionId);
        Assert.Single(submissions);Assert.True(userLocked);
    }
    [Fact] public async Task CompletedSubmissionIsPreservedInsteadOfCreatingAnotherDraft()
    {
        var first = await service.QuickCreateAsync(Request());submissions[0].SubmittedAt = DateTime.UtcNow;
        var second = await service.QuickCreateAsync(Request());
        Assert.Equal(first.Submission.SubmissionId, second.Submission.SubmissionId);
        Assert.NotNull(second.Submission.SubmittedAt);Assert.Single(submissions);
    }
    [Fact] public async Task EmptyRepoIsRejectedBeforeAnyUserOrDraftIsCreated()
    {
        hasQuestions = false;
        await Assert.ThrowsAsync<BadRequestException>(() => service.QuickCreateAsync(Request()));
        Assert.Null(user);Assert.Empty(submissions);
    }
    [Theory][InlineData(UserRole.Admin)][InlineData(UserRole.Lecturer)]
    public async Task ExistingPrivilegedAccountCannotBeDowngraded(UserRole role)
    {
        user = new() {Id = Guid.NewGuid(), Role = role, StudentCode = "SE192251", Email = "admin@example.com"};
        await Assert.ThrowsAsync<ConflictException>(() => service.QuickCreateAsync(Request()));
        Assert.Equal(role, user.Role);Assert.Empty(submissions);
    }
    [Fact] public async Task EmailCollisionDoesNotCreateOrReassignStudent()
    {
        collision = true;
        await Assert.ThrowsAsync<ConflictException>(() => service.QuickCreateAsync(Request()));
        Assert.Null(user);Assert.Empty(submissions);
    }
}
