using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using System.Linq.Expressions;
using Xunit;

namespace PACE.Tests;

public class LecturerCreateClassTests
{
    private readonly Guid _lecturerId = Guid.NewGuid();
    private readonly Guid _otherLecturerId = Guid.NewGuid();
    private readonly Guid _currentSemesterId = Guid.NewGuid();
    private readonly Guid _otherSemesterId = Guid.NewGuid();
    private readonly Guid _courseId = Guid.NewGuid();
    private readonly Guid _otherCourseId = Guid.NewGuid();

    private Semester GetCurrentSemester() => new()
    {
        Id = _currentSemesterId,
        Name = "Fall 2026",
        AcademicYear = "2026",
        StartDate = new DateOnly(2026, 9, 1),
        ActualStartDate = new DateOnly(2026, 9, 1),
        EndDate = new DateOnly(2026, 12, 31)
    };

    private Semester GetOtherSemester() => new()
    {
        Id = _otherSemesterId,
        Name = "Spring 2026",
        AcademicYear = "2026",
        StartDate = new DateOnly(2026, 1, 1),
        ActualStartDate = new DateOnly(2026, 1, 1),
        EndDate = new DateOnly(2026, 4, 30)
    };

    private Course GetLecturerCourse() => new()
    {
        Id = _courseId,
        Code = "SWP391",
        Name = "Software Project",
        Credits = 3,
        LecturerId = _lecturerId,
        Lecturer = new User { Id = _lecturerId, FullName = "Thầy Giảng", Email = "lecturer@pace.edu.vn" }
    };

    private Course GetOtherLecturerCourse() => new()
    {
        Id = _otherCourseId,
        Code = "PRN231",
        Name = "DotNet Apps",
        Credits = 3,
        LecturerId = _otherLecturerId,
        Lecturer = new User { Id = _otherLecturerId, FullName = "Thầy Khác", Email = "other@pace.edu.vn" }
    };

    private ClassService CreateClassService(
        UserRole role,
        Guid userId,
        bool classExistsInSemester = false,
        List<ClassImportRow>? importRows = null)
    {
        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "AnyAsync" => Task.FromResult(classExistsInSemester),
                "AddAsync" => Task.CompletedTask,
                _ => throw new InvalidOperationException($"IClassRepository method {m.Name} not mocked")
            };
            return res;
        });

        var semesterRepo = Stub<ISemesterRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetCurrentSemesterAsync" => Task.FromResult<Semester?>(GetCurrentSemester()),
                "GetByIdAsync" => Task.FromResult<Semester?>((Guid)a[0]! == _currentSemesterId ? GetCurrentSemester() : GetOtherSemester()),
                "GetAllOrderedAsync" => Task.FromResult(new List<Semester> { GetCurrentSemester(), GetOtherSemester() }),
                _ => throw new InvalidOperationException($"ISemesterRepository method {m.Name} not mocked")
            };
            return res;
        });

        var courseRepo = Stub<ICourseRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdWithLecturerAsync" => Task.FromResult<Course?>((Guid)a[0]! == _courseId ? GetLecturerCourse() : GetOtherLecturerCourse()),
                "GetByLecturerIdAsync" => Task.FromResult(new List<Course> { GetLecturerCourse() }),
                "GetAllWithLecturerAsync" => Task.FromResult(new List<Course> { GetLecturerCourse(), GetOtherLecturerCourse() }),
                "GetByCodeAsync" => Task.FromResult<Course?>((string)a[0]! == "SWP391" ? GetLecturerCourse() : null),
                _ => throw new InvalidOperationException($"ICourseRepository method {m.Name} not mocked")
            };
            return res;
        });

        var userRepo = Stub<IUserRepository>.Create((m, a) => Task.FromResult<User?>(null));

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Classes" => classRepo,
                "get_Semesters" => semesterRepo,
                "get_Courses" => courseRepo,
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
                "ReadClassList" => importRows ?? new List<ClassImportRow>
                {
                    new(2, "SE1703", "SWP391", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31))
                },
                "ExportClassTemplate" => new byte[] { 1, 2, 3, 4, 5 },
                _ => throw new InvalidOperationException($"IClassExcelService method {m.Name} not mocked")
            };
            return res;
        });

        return new ClassService(uow, current, excel);
    }

    [Fact]
    public async Task Lecturer_CanCreateClass_InCurrentSemester()
    {
        var service = CreateClassService(UserRole.Lecturer, _lecturerId);

        var request = new CreateClassRequest
        {
            Name = "SE1703",
            CourseId = _courseId
        };

        var created = await service.CreateClassAsync(request);

        Assert.NotNull(created);
        Assert.Equal("SE1703", created.Name);
        Assert.Equal(_courseId, created.CourseId);
        Assert.Equal("Fall 2026", created.Semester);
        Assert.Equal("Thầy Giảng", created.LecturerName);
    }

    [Fact]
    public async Task Lecturer_CannotCreateClass_ForOtherLecturersCourse()
    {
        var service = CreateClassService(UserRole.Lecturer, _lecturerId);

        var request = new CreateClassRequest
        {
            Name = "SE1704",
            CourseId = _otherCourseId // thuộc về _otherLecturerId
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateClassAsync(request));
    }

    [Fact]
    public async Task Lecturer_CannotCreateClass_InDifferentSemester()
    {
        var service = CreateClassService(UserRole.Lecturer, _lecturerId);

        var request = new CreateClassRequest
        {
            Name = "SE1703",
            CourseId = _courseId,
            SemesterId = _otherSemesterId // không phải kỳ hiện tại
        };

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateClassAsync(request));
    }

    [Fact]
    public async Task Lecturer_CannotCreateClass_WithDuplicateName()
    {
        var service = CreateClassService(UserRole.Lecturer, _lecturerId, classExistsInSemester: true);

        var request = new CreateClassRequest
        {
            Name = "SE1703",
            CourseId = _courseId
        };

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateClassAsync(request));
    }

    [Fact]
    public async Task Student_CannotCreateClass()
    {
        var service = CreateClassService(UserRole.Student, Guid.NewGuid());

        var request = new CreateClassRequest
        {
            Name = "SE1703",
            CourseId = _courseId
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateClassAsync(request));
    }

    [Fact]
    public async Task ImportClasses_Success_ForLecturer()
    {
        var rows = new List<ClassImportRow>
        {
            new(2, "SE1703", "SWP391", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31))
        };
        var service = CreateClassService(UserRole.Lecturer, _lecturerId, importRows: rows);

        using var ms = new MemoryStream([1, 2, 3]);
        var result = await service.ImportClassesAsync(ms);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Empty(result.Errors);
        Assert.Single(result.CreatedClasses);
    }

    [Fact]
    public async Task ImportClasses_SkipsInvalidCourse()
    {
        var rows = new List<ClassImportRow>
        {
            new(2, "SE1703", "NONEXISTENT", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31))
        };
        var service = CreateClassService(UserRole.Lecturer, _lecturerId, importRows: rows);

        using var ms = new MemoryStream([1, 2, 3]);
        var result = await service.ImportClassesAsync(ms);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ExportClassTemplate_ReturnsValidFile()
    {
        var service = CreateClassService(UserRole.Lecturer, _lecturerId);

        var (bytes, fileName) = await service.ExportClassTemplateAsync();

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
        Assert.EndsWith(".xlsx", fileName);
    }

    [Fact]
    public async Task SemesterService_AdminCanCreateSemester()
    {
        var semesterRepo = Stub<ISemesterRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "AnyAsync" => Task.FromResult(false),
                "AddAsync" => Task.CompletedTask,
                "GetCurrentSemesterAsync" => Task.FromResult<Semester?>(GetCurrentSemester()),
                _ => throw new InvalidOperationException($"ISemesterRepository method {m.Name} not mocked")
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Semesters" => semesterRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => throw new InvalidOperationException($"UoW property/method {m.Name} not mocked")
            };
            return res;
        });

        var semesterService = new SemesterService(uow);
        var created = await semesterService.CreateAsync(new CreateSemesterRequest
        {
            Name = "Spring 2027",
            AcademicYear = "2027",
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 4, 30)
        });

        Assert.NotNull(created);
        Assert.Equal("Spring 2027", created.Name);
    }

    [Fact]
    public async Task SemesterService_ThrowsWhenDatesAreInvalid()
    {
        var semesterRepo = Stub<ISemesterRepository>.Create((m, a) => Task.CompletedTask);
        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Semesters" => semesterRepo,
                _ => throw new InvalidOperationException()
            };
            return res;
        });

        var semesterService = new SemesterService(uow);

        await Assert.ThrowsAsync<BadRequestException>(() => semesterService.CreateAsync(new CreateSemesterRequest
        {
            Name = "Invalid Dates",
            AcademicYear = "2027",
            StartDate = new DateOnly(2027, 5, 1),
            EndDate = new DateOnly(2027, 4, 30) // End < Start
        }));
    }

    [Fact]
    public void ClosedXml_ExportTemplateAndReadBack_MatchesExpectedColumns()
    {
        var excelService = new PACE.Infrastructure.Excel.ClosedXmlClassExcelService();
        var templateBytes = excelService.ExportClassTemplate();
        Assert.NotNull(templateBytes);
        Assert.NotEmpty(templateBytes);

        using var ms = new MemoryStream(templateBytes);
        var rows = excelService.ReadClassList(ms);

        Assert.NotEmpty(rows);
        Assert.Contains(rows, r => r.ClassName == "SE1703" && r.CourseCode == "SWP391");
    }
}
