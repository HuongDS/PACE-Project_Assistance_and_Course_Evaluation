using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Dtos.StudentAnswer;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using Xunit;

namespace PACE.Tests;

public class RepositoryStudentTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = "admin@pace.edu.vn";
        public UserRole Role { get; set; } = UserRole.Admin;
        public Guid? AnalysisRunId { get; set; }
        public bool IsAuthenticated => true;
    }

    private class FakeBusinessService : IBusinessService
    {
        public Task<IReadOnlyList<BusinessDto>> GetByRunAsync(Guid analysisRunId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BusinessDto>>([]);
        public Task<BusinessDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<CodeFlowDto?> GetCodeBaseWithBusinessAsync(Guid businessId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task AssignStudents_CreatesNew_MovesExisting_AndDetectsAlreadyAssigned()
    {
        var repo1 = new AnalysisRun { Id = Guid.NewGuid(), RepoName = "Repo 1", RepoUrl = "https://github.com/org/repo1" };
        var repo2 = new AnalysisRun { Id = Guid.NewGuid(), RepoName = "Repo 2", RepoUrl = "https://github.com/org/repo2" };
        var runs = new List<AnalysisRun> { repo1, repo2 };

        var student1 = new User { Id = Guid.NewGuid(), StudentCode = "SE1001", Email = "se1001@gmail.com", Role = UserRole.Student, AnalysisRunId = repo1.Id };
        var student2 = new User { Id = Guid.NewGuid(), StudentCode = "SE1002", Email = "se1002@gmail.com", Role = UserRole.Student, AnalysisRunId = repo2.Id };
        var users = new List<User> { student1, student2 };

        var userRepo = Stub<IUserRepository>.Create((m, a) => m.Name switch
        {
            "GetByStudentCodeAsync" => Task.FromResult<User?>(users.FirstOrDefault(u => string.Equals(u.StudentCode, (string)a[0]!, StringComparison.OrdinalIgnoreCase))),
            "GetWithRepoAsync" => Task.FromResult<User?>(users.FirstOrDefault(u => u.Id == (Guid)a[0]!)),
            "EmailExistsAsync" => Task.FromResult(users.Any(u => string.Equals(u.Email, (string)a[0]!, StringComparison.OrdinalIgnoreCase))),
            "AddAsync" => Task.Run(() => { users.Add((User)a[0]!); return Task.CompletedTask; }),
            "SearchAsync" => Task.FromResult(new PagedResult<User>
            {
                Items = users.Where(u => u.Role == (UserRole?)a[0] && (a[1] == null || u.AnalysisRunId == (Guid?)a[1])).ToList(),
                TotalCount = users.Count(u => u.Role == (UserRole?)a[0] && (a[1] == null || u.AnalysisRunId == (Guid?)a[1])),
                Page = 1,
                PageSize = 20
            }),
            _ => throw new NotSupportedException(m.Name)
        });

        var runRepo = Stub<IAnalysisRunRepository>.Create((m, a) => m.Name switch
        {
            "GetByIdAsync" => Task.FromResult<AnalysisRun?>(runs.FirstOrDefault(r => r.Id == (Guid)a[0]!)),
            _ => throw new NotSupportedException(m.Name)
        });

        var uow = Stub<IUnitOfWork>.Create((m, _) => m.Name switch
        {
            "get_Users" => userRepo,
            "get_AnalysisRuns" => runRepo,
            "SaveChangesAsync" => Task.FromResult(1),
            _ => throw new NotSupportedException(m.Name)
        });

        var service = new AnalysisRunService(uow, new FakeCurrentUserService(), new FakeBusinessService());

        var result = await service.AssignStudentsAsync(repo1.Id, new AssignRepositoryStudentsRequest
        {
            StudentCodes = ["SE1001", "SE1002", "SE1003"]
        });

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.AlreadyAssigned);
        Assert.Equal(1, result.Moved);
        Assert.Equal(1, result.Created);

        var students = await service.GetRepositoryStudentsAsync(repo1.Id, new PageQuery { Page = 1, PageSize = 20 });
        Assert.Equal(3, students.TotalCount);
    }

    [Fact]
    public async Task RemoveStudent_DetachesStudentFromRepo()
    {
        var repo = new AnalysisRun { Id = Guid.NewGuid(), RepoName = "Repo A", RepoUrl = "https://github.com/org/repo-a" };
        var student = new User { Id = Guid.NewGuid(), StudentCode = "SE9999", Email = "se9999@gmail.com", Role = UserRole.Student, AnalysisRunId = repo.Id };
        var users = new List<User> { student };

        var userRepo = Stub<IUserRepository>.Create((m, a) => m.Name switch
        {
            "GetWithRepoAsync" => Task.FromResult<User?>(users.FirstOrDefault(u => u.Id == (Guid)a[0]!)),
            "SearchAsync" => Task.FromResult(new PagedResult<User>
            {
                Items = users.Where(u => u.AnalysisRunId == repo.Id).ToList(),
                TotalCount = users.Count(u => u.AnalysisRunId == repo.Id),
                Page = 1,
                PageSize = 20
            }),
            _ => throw new NotSupportedException(m.Name)
        });

        var runRepo = Stub<IAnalysisRunRepository>.Create((m, a) => m.Name switch
        {
            "GetByIdAsync" => Task.FromResult<AnalysisRun?>(a[0] is Guid id && id == repo.Id ? repo : null),
            _ => throw new NotSupportedException(m.Name)
        });

        var uow = Stub<IUnitOfWork>.Create((m, _) => m.Name switch
        {
            "get_Users" => userRepo,
            "get_AnalysisRuns" => runRepo,
            "SaveChangesAsync" => Task.FromResult(1),
            _ => throw new NotSupportedException(m.Name)
        });

        var service = new AnalysisRunService(uow, new FakeCurrentUserService(), new FakeBusinessService());

        await service.RemoveStudentAsync(repo.Id, student.Id);
        Assert.Null(student.AnalysisRunId);

        var students = await service.GetRepositoryStudentsAsync(repo.Id, new PageQuery { Page = 1, PageSize = 20 });
        Assert.Equal(0, students.TotalCount);
    }

    [Fact]
    public async Task AssignStudents_ThrowsNotFound_WhenRepoDoesNotExist()
    {
        var runRepo = Stub<IAnalysisRunRepository>.Create((m, _) => m.Name switch
        {
            "GetByIdAsync" => Task.FromResult<AnalysisRun?>(null),
            _ => throw new NotSupportedException(m.Name)
        });

        var uow = Stub<IUnitOfWork>.Create((m, _) => m.Name switch
        {
            "get_AnalysisRuns" => runRepo,
            _ => throw new NotSupportedException(m.Name)
        });

        var service = new AnalysisRunService(uow, new FakeCurrentUserService(), new FakeBusinessService());

        await Assert.ThrowsAsync<NotFoundException>(() => service.AssignStudentsAsync(Guid.NewGuid(), new AssignRepositoryStudentsRequest
        {
            StudentCodes = ["SE123456"]
        }));
    }
}
