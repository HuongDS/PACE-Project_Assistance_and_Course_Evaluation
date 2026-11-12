using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using Xunit;

namespace PACE.Tests;

public class ClassServiceTests
{
    private readonly Guid _classId = Guid.NewGuid();
    private readonly Guid _otherClassId = Guid.NewGuid();
    private readonly Guid _lecturerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private const string StudentCode = "SE170123";

    private ClassService CreateService(
        UserRole role,
        Guid userId,
        bool isLecturer = true,
        bool isStudent = true)
    {
        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "ClassExistsAsync" => Task.FromResult((Guid)a[0]! == _classId),
                "IsLecturerOfClassAsync" => Task.FromResult((Guid)a[0]! == _classId && isLecturer),
                "IsStudentOfClassAsync" => Task.FromResult((Guid)a[0]! == _classId && isStudent),
                "GetStudentCodeByUserIdAsync" => Task.FromResult<string?>((Guid)a[0]! == _studentId ? StudentCode : null),
                "SearchClassesAsync" => Task.FromResult(new PagedResult<ClassDto>
                {
                    Items = [new ClassDto(_classId, "SE1701", Guid.NewGuid(), "PRN231", "DotNet", _lecturerId, "Thầy Giảng", "lecturer@fpt.edu.vn", "FA26", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(4)), 30, 6)],
                    Page = 1,
                    PageSize = 10,
                    TotalCount = 1
                }),
                "GetClassDetailAsync" => Task.FromResult<ClassDetailDto?>((Guid)a[0]! == _classId
                    ? new ClassDetailDto(
                        new ClassDto(_classId, "SE1701", Guid.NewGuid(), "PRN231", "DotNet", _lecturerId, "Thầy Giảng", "lecturer@fpt.edu.vn", "FA26", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(4)), 1, 1),
                        [new ClassStudentDto(StudentCode, _studentId, "Sinh viên A", "se170123@fpt.edu.vn", 1, "Nhóm 1", DateTime.UtcNow)],
                        [new ClassGroupDto(Guid.NewGuid(), "Nhóm 1", "Đề tài A", null, null, null, 1)])
                    : null),
                "GetClassStudentsAsync" => Task.FromResult(new PagedResult<ClassStudentDto>
                {
                    Items = [new ClassStudentDto(StudentCode, _studentId, "Sinh viên A", "se170123@fpt.edu.vn", 1, "Nhóm 1", DateTime.UtcNow)],
                    Page = 1,
                    PageSize = 10,
                    TotalCount = 1
                }),
                "GetAllStudentsInClassAsync" => Task.FromResult(new List<ClassStudentDto>
                {
                    new(StudentCode, _studentId, "Sinh viên A", "se170123@fpt.edu.vn", 1, "Nhóm 1", DateTime.UtcNow)
                }),
                "AddStudentsToClassAsync" => Task.FromResult(1),
                "RemoveStudentFromClassAsync" => Task.FromResult((string)a[1]! == StudentCode),
                _ => throw new InvalidOperationException($"IClassRepository method {m.Name} not mocked")
            };
            return res;
        });

        var userRepo = Stub<IUserRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByStudentCodeAsync" => Task.FromResult<User?>(null),
                "GetByEmailAsync" => Task.FromResult<User?>(null),
                "AddAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException($"IUserRepository method {m.Name} not mocked")
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Classes" => classRepo,
                "get_Users" => userRepo,
                "SaveChangesAsync" => Task.FromResult(1),
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
                "get_IsAdmin" => role == UserRole.Admin,
                _ => throw new InvalidOperationException($"CurrentUserService {m.Name} not mocked")
            };
            return res;
        });

        var excel = Stub<IClassExcelService>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "ReadStudentList" => new List<ClassStudentImportRow>
                {
                    new(2, "SE170123", "Sinh viên A", "se170123@fpt.edu.vn"),
                    new(3, "SE170456", "Sinh viên B", "se170456@fpt.edu.vn")
                },
                "ExportStudentList" => new byte[] { 1, 2, 3, 4 },
                _ => throw new InvalidOperationException($"IClassExcelService method {m.Name} not mocked")
            };
            return res;
        });

        return new ClassService(uow, current, excel);
    }

    [Fact]
    public async Task Admin_CanAccessAllClassesAndDetails()
    {
        var service = CreateService(UserRole.Admin, Guid.NewGuid());

        var paged = await service.GetPagedAsync(new ClassQuery());
        Assert.Single(paged.Items);

        var detail = await service.GetDetailAsync(_classId);
        Assert.Equal(_classId, detail.Class.Id);
    }

    [Fact]
    public async Task Lecturer_CanAccessOwnClass_ThrowsForbiddenOnOtherClass()
    {
        var service = CreateService(UserRole.Lecturer, _lecturerId, isLecturer: true);
        var detail = await service.GetDetailAsync(_classId);
        Assert.Equal(_classId, detail.Class.Id);

        var unauthorizedService = CreateService(UserRole.Lecturer, _lecturerId, isLecturer: false);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            unauthorizedService.GetDetailAsync(_classId));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            unauthorizedService.AddStudentsAsync(_classId, new AddStudentsToClassRequest { StudentCodes = ["SE999999"] }));
    }

    [Fact]
    public async Task Student_CanAccessEnrolledClass_ThrowsForbiddenOnOtherClass()
    {
        var service = CreateService(UserRole.Student, _studentId, isStudent: true);
        var detail = await service.GetDetailAsync(_classId);
        Assert.Equal(_classId, detail.Class.Id);

        var unauthorizedService = CreateService(UserRole.Student, _studentId, isStudent: false);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            unauthorizedService.GetDetailAsync(_classId));

        // Student cannot mutate student list
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.AddStudentsAsync(_classId, new AddStudentsToClassRequest { StudentCodes = ["SE999999"] }));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RemoveStudentAsync(_classId, StudentCode));
    }

    [Fact]
    public async Task Lecturer_CanAddAndRemoveStudent()
    {
        var service = CreateService(UserRole.Lecturer, _lecturerId, isLecturer: true);

        var added = await service.AddStudentsAsync(_classId, new AddStudentsToClassRequest
        {
            StudentCodes = ["SE170123"]
        });
        Assert.Equal(1, added);

        await service.RemoveStudentAsync(_classId, StudentCode);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.RemoveStudentAsync(_classId, "NON_EXISTING"));
    }

    [Fact]
    public async Task ImportAndExportStudents_Success()
    {
        var service = CreateService(UserRole.Lecturer, _lecturerId, isLecturer: true);

        using var ms = new MemoryStream();
        var importResult = await service.ImportStudentsAsync(_classId, ms);
        Assert.Equal(2, importResult.TotalRows);

        var exportResult = await service.ExportStudentsAsync(_classId);
        Assert.NotEmpty(exportResult.FileBytes);
        Assert.Contains("SE1701", exportResult.FileName);
    }
}

