using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using Xunit;

namespace PACE.Tests;

public class AnalysisRunAuthorizationTests
{
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _lecturerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();

    private readonly Guid _myClassId = Guid.NewGuid();
    private readonly Guid _otherClassId = Guid.NewGuid();

    private readonly Guid _myRepoId = Guid.NewGuid();
    private readonly Guid _otherRepoId = Guid.NewGuid();

    private AnalysisRunService CreateService(
        UserRole role,
        Guid userId,
        bool isLecturerOfClass = true,
        bool isLecturerOfRepo = true,
        bool isStudentOfRepo = true,
        Guid? studentRepoId = null)
    {
        var runs = Stub<IAnalysisRunRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "IsLecturerOfClassAsync" => Task.FromResult((Guid)a[0]! == _myClassId && isLecturerOfClass),
                "IsLecturerOfRepoClassAsync" => Task.FromResult((Guid)a[0]! == _myRepoId && isLecturerOfRepo),
                "GetLecturerClassIdsAsync" => Task.FromResult<IReadOnlyList<Guid>>(isLecturerOfClass ? [_myClassId] : []),
                "IsStudentOfRepoAsync" => Task.FromResult((Guid)a[0]! == _myRepoId && isStudentOfRepo),
                "GetStudentRepoIdAsync" => Task.FromResult(studentRepoId),
                "GetByIdAsync" => Task.FromResult<AnalysisRun?>(new AnalysisRun
                {
                    Id = (Guid)a[0]!,
                    RepoUrl = "https://github.com/test/repo",
                    RepoName = "test-repo"
                }),
                "GetSummaryAsync" => Task.FromResult<AnalysisRunSummary?>(new AnalysisRunSummary(
                    (Guid)a[0]!, "https://github.com/test/repo", "test-repo", DateTime.UtcNow, 1, 1, 1,
                    "Group 1", "Topic 1", _myClassId, "Class 1")),
                "SearchSummariesAsync" => Task.FromResult(new PagedResult<AnalysisRunSummary>
                {
                    Items = [new AnalysisRunSummary(_myRepoId, "https://github.com/test/repo", "test-repo", DateTime.UtcNow, 1, 1, 1, "Group 1", "Topic 1", _myClassId, "Class 1")],
                    Page = 1,
                    PageSize = 10,
                    TotalCount = 1
                }),
                "DeleteAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException($"Method {m.Name} not mocked")
            };
            return res;
        });

        var users = Stub<IUserRepository>.Create((m, a) => m.Name switch
        {
            "GetEmailsByRepoAsync" => Task.FromResult(new List<string>()),
            _ => throw new InvalidOperationException($"User method {m.Name} not mocked")
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_AnalysisRuns" => runs,
                "get_Users" => users,
                "SaveChangesAsync" => Task.FromResult(1),
                "ExecuteInTransactionAsync" => ((Func<CancellationToken, Task>)a[0]!)(default),
                _ => throw new InvalidOperationException($"UoW property/method {m.Name} not mocked")
            };
            return res;
        });

        var current = Stub<ICurrentUserService>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_IsAuthenticated" => true,
                "get_Role" => role,
                "get_UserId" => userId,
                "get_AnalysisRunId" => (Guid?)null,
                _ => throw new InvalidOperationException($"CurrentUserService {m.Name} not mocked")
            };
            return res;
        });

        return new AnalysisRunService(uow, current, Stub<IBusinessService>.Create((m, a) => Task.FromResult<IReadOnlyList<BusinessDto>>([])));
    }

    [Fact]
    public async Task Admin_CanAccessAnyRepoDetail_AndGetAll()
    {
        var service = CreateService(UserRole.Admin, _adminId);

        var paged = await service.GetPagedAsync(new AnalysisRunQuery());
        Assert.Single(paged.Items);

        var detail = await service.GetDetailAsync(_otherRepoId);
        Assert.Equal(_otherRepoId, detail.Run.Id);
    }

    [Fact]
    public async Task Lecturer_CanAccessRepoInTaughtClass_AndPagedOfTaughtClass()
    {
        var service = CreateService(UserRole.Lecturer, _lecturerId, isLecturerOfClass: true, isLecturerOfRepo: true);

        var paged = await service.GetPagedAsync(new AnalysisRunQuery { ClassId = _myClassId });
        Assert.Single(paged.Items);

        var detail = await service.GetDetailAsync(_myRepoId);
        Assert.Equal(_myRepoId, detail.Run.Id);
    }

    [Fact]
    public async Task Lecturer_ThrowsForbidden_WhenAccessingOtherClassOrRepo()
    {
        var service = CreateService(UserRole.Lecturer, _lecturerId, isLecturerOfClass: false, isLecturerOfRepo: false);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetPagedAsync(new AnalysisRunQuery { ClassId = _otherClassId }));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetDetailAsync(_otherRepoId));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.DeleteAsync(_otherRepoId));
    }

    [Fact]
    public async Task Student_CanOnlyAccessTheirOwnRepo()
    {
        var service = CreateService(UserRole.Student, _studentId, isStudentOfRepo: true, studentRepoId: _myRepoId);

        var detail = await service.GetDetailAsync(_myRepoId);
        Assert.Equal(_myRepoId, detail.Run.Id);

        var paged = await service.GetPagedAsync(new AnalysisRunQuery());
        Assert.Single(paged.Items);
    }

    [Fact]
    public async Task Student_ThrowsForbidden_WhenAccessingOtherRepoOrManagingRepo()
    {
        var service = CreateService(UserRole.Student, _studentId, isStudentOfRepo: false, studentRepoId: _myRepoId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.GetDetailAsync(_otherRepoId));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.DeleteAsync(_myRepoId));
    }
}
