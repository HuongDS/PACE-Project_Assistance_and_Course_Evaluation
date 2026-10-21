using System.Linq.Expressions;
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

public class RepositoryStudentTests
{
    [Fact] public async Task BulkCreatesMovesAndDeduplicatesWithoutReplacingExistingEmailOrSubmissions()
    {
        var f = new Fixture();
        var old = f.Student("SE123456", "real@gmail.com", Guid.NewGuid());
        var submission = new StudentSubmission { Id = Guid.NewGuid(), AnalysisRunId = old.AnalysisRunId!.Value };
        old.Submissions.Add(submission);
        var result = await f.Service.AssignStudentsAsync(f.Run.Id, new() { StudentCodes = [" se123456 ", "SE123456", "SE654321"] });
        Assert.Equal(new AssignRepositoryStudentsResult(2, 1, 1, 0), result);
        Assert.Equal("real@gmail.com", old.Email);
        Assert.Equal(f.Run.Id, old.AnalysisRunId);
        Assert.Same(f.Run, old.AnalysisRun);
        Assert.Same(submission, Assert.Single(old.Submissions));
        Assert.Equal("se654321@gmail.com", f.Users.Single(u => u.StudentCode == "SE654321").Email);
        var retry = await f.Service.AssignStudentsAsync(f.Run.Id, new() { StudentCodes = ["SE123456", "SE654321"] });
        Assert.Equal(2, retry.AlreadyAssigned);
        Assert.Equal(2, f.Users.Count);
    }

    [Fact] public async Task RemovingOnlyDetachesAccountAndPreservesHistoricalSubmission()
    {
        var f = new Fixture(); var student = f.Student("SE123456", "real@gmail.com", f.Run.Id);
        student.Submissions.Add(new StudentSubmission { Id = Guid.NewGuid(), AnalysisRunId = f.Run.Id });
        await f.Service.RemoveStudentAsync(f.Run.Id, student.Id);
        Assert.Null(student.AnalysisRunId);
        Assert.Null(student.AnalysisRun);
        Assert.Equal("real@gmail.com", student.Email);
        Assert.Equal(f.Run.Id, Assert.Single(student.Submissions).AnalysisRunId);
        Assert.Single(f.Users);
    }

    [Fact] public async Task StaleRemovalCannotDetachFromAnotherRepo()
    {
        var f = new Fixture(); var student = f.Student("SE123456", "real@gmail.com", Guid.NewGuid());
        var previous = student.AnalysisRunId;
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.RemoveStudentAsync(f.Run.Id, student.Id));
        Assert.Equal(previous, student.AnalysisRunId); Assert.Equal(0, f.Commits);
    }

    [Fact] public async Task StaffAccountsAndDefaultEmailCollisionsAreRejectedWithoutCommit()
    {
        var f = new Fixture(); var staff = f.Student("SE123456", "admin@gmail.com", null); staff.Role = UserRole.Admin;
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.AssignStudentsAsync(f.Run.Id, new() { StudentCodes = ["SE123456"] }));
        Assert.Equal(UserRole.Admin, staff.Role); Assert.Equal(0, f.Commits);
        f.Student("SE777777", "se654321@gmail.com", null);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.AssignStudentsAsync(f.Run.Id, new() { StudentCodes = ["SE654321"] }));
        Assert.DoesNotContain(f.Users, u => u.StudentCode == "SE654321"); Assert.Equal(0, f.Commits);
    }

    [Fact] public async Task StudentRoleCannotUseMembershipMutations()
    {
        var f = new Fixture { Role = UserRole.Student };
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.AssignStudentsAsync(f.Run.Id, new() { StudentCodes = ["SE123456"] }));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.RemoveStudentAsync(f.Run.Id, Guid.NewGuid()));
        Assert.Equal(0, f.Commits);
    }

    [Fact] public async Task MissingRepoAndInvalidCodesDoNotCreateAccounts()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<BadRequestException>(() => f.Service.AssignStudentsAsync(f.Run.Id, new() { StudentCodes = ["bad@email.com"] }));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.AssignStudentsAsync(Guid.NewGuid(), new() { StudentCodes = ["SE123456"] }));
        Assert.Empty(f.Users); Assert.Equal(0, f.Commits);
    }

    [Fact] public async Task RemovedStudentCannotReadOldRepoThroughStaleJwtClaim()
    {
        var f = new Fixture(); f.Student("SE123456", "real@gmail.com", null);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.GetMineAsync());
    }

    private sealed class Fixture
    {
        public readonly AnalysisRun Run = new() { Id = Guid.NewGuid(), RepoUrl = "https://github.com/test/repo" };
        public readonly List<User> Users = [];
        public UserRole Role = UserRole.Admin;
        public int Commits;
        public AnalysisRunService Service;
        public Fixture()
        {
            var users = Stub<IUserRepository>.Create((m,a) => m.Name switch {
                "FindAsync" => Task.FromResult(Users.Where(((Expression<Func<User,bool>>)a[0]!).Compile()).ToList()),
                "GetWithRepoAsync" => Task.FromResult(Users.FirstOrDefault(u => u.Id == (Guid)a[0]!)),
                "EmailExistsAsync" => Task.FromResult(Users.Any(u => u.Email.Equals((string)a[0]!, StringComparison.OrdinalIgnoreCase))),
                "AddAsync" => Add((User)a[0]!),
                _ => throw new InvalidOperationException(m.Name) });
            var runs = Stub<IAnalysisRunRepository>.Create((m,a) => m.Name == "GetByIdAsync" ? Task.FromResult((Guid)a[0]! == Run.Id ? Run : null) : throw new InvalidOperationException(m.Name));
            var locks = Stub<IStudentSubmissionRepository>.Create((m,a) => m.Name is "LockUserAsync" or "LockQuickCreateKeyAsync" ? Task.CompletedTask : throw new InvalidOperationException(m.Name));
            var uow = Stub<IUnitOfWork>.Create((m,a) => m.Name switch {
                "get_Users" => users, "get_AnalysisRuns" => runs, "get_StudentSubmissions" => locks,
                "ExecuteInTransactionAsync" => m.IsGenericMethod ? Transaction((Func<CancellationToken,Task<AssignRepositoryStudentsResult>>)a[0]!) : Transaction((Func<CancellationToken,Task>)a[0]!),
                _ => throw new InvalidOperationException(m.Name) });
            var current = Stub<ICurrentUserService>.Create((m,a) => m.Name switch {
                "get_IsAuthenticated" => true, "get_Role" => Role, "get_UserId" => Users.FirstOrDefault()?.Id ?? Guid.NewGuid(), "get_AnalysisRunId" => Run.Id,
                _ => throw new InvalidOperationException(m.Name) });
            Service = new(uow, current, Stub<IBusinessService>.Create((m,a) => throw new NotImplementedException()));
        }
        private Task Add(User user) { Users.Add(user); return Task.CompletedTask; }
        private async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> callback) { var result = await callback(default); Commits++; return result; }
        private async Task Transaction(Func<CancellationToken,Task> callback) { await callback(default); Commits++; }
        public User Student(string code, string email, Guid? repo) { var u = new User { Id=Guid.NewGuid(),StudentCode=code,Email=email,Role=UserRole.Student,AnalysisRunId=repo }; Users.Add(u); return u; }
    }
}
