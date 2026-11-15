using System.Reflection;
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

public class AcademicCrudTests
{
    private static ICurrentUserService MockUser(UserRole role, Guid userId)
    {
        return Stub<ICurrentUserService>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_IsAuthenticated" => true,
                "get_Role" => role,
                "get_UserId" => userId,
                "get_IsAdmin" => role == UserRole.Admin,
                _ => null!
            };
            return res;
        });
    }

    [Fact]
    public async Task SemesterService_DeleteAsync_BlocksWhenClassesExist()
    {
        var semesterId = Guid.NewGuid();
        var semester = new Semester { Id = semesterId, Name = "Fall 2026", AcademicYear = "2026", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 12, 31) };

        var semRepo = Stub<ISemesterRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Semester?>(semester),
                "Remove" => null!,
                _ => null!
            };
            return res;
        });

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "AnyAsync" => Task.FromResult(true), // classes exist!
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Semesters" => semRepo,
                "get_Classes" => classRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var service = new SemesterService(uow);

        await Assert.ThrowsAsync<BadRequestException>(() => service.DeleteAsync(semesterId));
    }

    [Fact]
    public async Task SemesterService_DeleteAsync_SucceedsWhenNoClasses()
    {
        var semesterId = Guid.NewGuid();
        var semester = new Semester { Id = semesterId, Name = "Spring 2027", AcademicYear = "2027", StartDate = new DateOnly(2027, 1, 1), EndDate = new DateOnly(2027, 5, 1) };
        bool removed = false;

        var semRepo = Stub<ISemesterRepository>.Create((m, a) =>
        {
            if (m.Name == "GetByIdAsync") return Task.FromResult<Semester?>(semester);
            if (m.Name == "Remove") { removed = true; return null!; }
            return null!;
        });

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "AnyAsync" => Task.FromResult(false), // no classes!
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Semesters" => semRepo,
                "get_Classes" => classRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var service = new SemesterService(uow);
        await service.DeleteAsync(semesterId);

        Assert.True(removed);
    }

    [Fact]
    public async Task ClassService_UpdateClassAsync_UpdatesAndValidates()
    {
        var classId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var cls = new Class
        {
            Id = classId,
            Name = "SE1701",
            CourseId = courseId,
            SemesterId = semesterId,
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var course = new Course
        {
            Id = courseId,
            Code = "PRN231",
            Name = "DotNet",
            LecturerId = lecturerId,
            Lecturer = new User { Id = lecturerId, FullName = "Thầy Giảng", Email = "giang@fpt.edu.vn" }
        };

        var semester = new Semester
        {
            Id = semesterId,
            Name = "Fall 2026",
            AcademicYear = "2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Class?>(cls),
                "AnyAsync" => Task.FromResult(false),
                "Update" => null!,
                "GetAllStudentsInClassAsync" => Task.FromResult(new List<ClassStudentDto>()),
                _ => null!
            };
            return res;
        });

        var courseRepo = Stub<ICourseRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Course?>(course),
                _ => null!
            };
            return res;
        });

        var semRepo = Stub<ISemesterRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Semester?>(semester),
                _ => null!
            };
            return res;
        });

        var groupRepo = Stub<IGroupRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "CountAsync" => Task.FromResult(0),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Classes" => classRepo,
                "get_Courses" => courseRepo,
                "get_Semesters" => semRepo,
                "get_Groups" => groupRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var current = MockUser(UserRole.Admin, Guid.NewGuid());
        var excel = Stub<IClassExcelService>.Create((m, a) => null!);

        var service = new ClassService(uow, current, excel);

        var request = new UpdateClassRequest
        {
            Name = "SE1701-NEW",
            CourseId = courseId,
            SemesterId = semesterId,
            StartDate = new DateOnly(2026, 9, 5),
            EndDate = new DateOnly(2026, 12, 25)
        };

        var updated = await service.UpdateClassAsync(classId, request);

        Assert.Equal("SE1701-NEW", updated.Name);
        Assert.Equal(courseId, updated.CourseId);
    }

    [Fact]
    public async Task GroupService_CreateAndUpdate_Succeeds()
    {
        var classId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        Group? storedGroup = null;

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "ClassExistsAsync" => Task.FromResult(true),
                _ => null!
            };
            return res;
        });

        var groupRepo = Stub<IGroupRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "AnyAsync" => Task.FromResult(false),
                "AddAsync" => Task.Run(() => storedGroup = (Group)a[0]!),
                "GetByIdAsync" => Task.FromResult(storedGroup),
                "Update" => null!,
                "GetGroupMembersAsync" => Task.FromResult(new List<GroupMemberDto>()),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Classes" => classRepo,
                "get_Groups" => groupRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var current = MockUser(UserRole.Admin, Guid.NewGuid());
        var service = new GroupService(uow, current);

        var created = await service.CreateAsync(new CreateGroupRequest
        {
            ClassId = classId,
            Name = "Team Alpha",
            TopicName = "PACE System"
        });

        Assert.NotNull(created);
        Assert.Equal("Team Alpha", created.Name);

        var updated = await service.UpdateAsync(created.Id, new UpdateGroupRequest
        {
            Name = "Team Alpha Plus",
            TopicName = "PACE 2.0"
        });

        Assert.Equal("Team Alpha Plus", updated.Name);
        Assert.Equal("PACE 2.0", updated.TopicName);
    }

    [Fact]
    public async Task TimetableService_RoomAndTimeSlot_Crud()
    {
        var rooms = new List<Room>();
        var slots = new List<TimeSlot>();

        var timetableRepo = Stub<ITimetableRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "RoomCodeExistsAsync" => Task.FromResult(false),
                "AddRoomAsync" => Task.Run(() => rooms.Add((Room)a[0]!)),
                "GetRoomByIdAsync" => Task.FromResult(rooms.FirstOrDefault(r => r.Id == (Guid)a[0]!)),
                "TimeSlotPeriodExistsAsync" => Task.FromResult(false),
                "AddTimeSlotAsync" => Task.Run(() => slots.Add((TimeSlot)a[0]!)),
                "GetTimeSlotByIdAsync" => Task.FromResult(slots.FirstOrDefault(s => s.Id == (Guid)a[0]!)),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Timetables" => timetableRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var current = MockUser(UserRole.Admin, Guid.NewGuid());
        var service = new TimetableService(uow, current);

        var room = await service.CreateRoomAsync(new CreateRoomRequest { Code = "BE-201", Building = "Beta Building" });
        Assert.Equal("BE-201", room.Code);

        var slot = await service.CreateTimeSlotAsync(new CreateTimeSlotRequest
        {
            PeriodNumber = 1,
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(9, 0)
        });
        Assert.Equal(1, slot.PeriodNumber);
    }

    [Fact]
    public async Task ProfileService_StudentAndLecturerProfiles_Crud()
    {
        var studentProfiles = new List<StudentProfile>();
        var lecturerProfiles = new List<LecturerProfile>();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        var profileRepo = Stub<IProfileRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "StudentCodeExistsAsync" => Task.FromResult(false),
                "GetStudentProfileByUserIdAsync" => Task.FromResult<StudentProfile?>(null),
                "AddStudentProfileAsync" => Task.Run(() => studentProfiles.Add((StudentProfile)a[0]!)),
                "GetStudentProfileByCodeAsync" => Task.FromResult(studentProfiles.FirstOrDefault(s => s.StudentCode == (string)a[0]!)),
                "LecturerCodeExistsAsync" => Task.FromResult(false),
                "GetLecturerProfileByUserIdAsync" => Task.FromResult<LecturerProfile?>(null),
                "AddLecturerProfileAsync" => Task.Run(() => lecturerProfiles.Add((LecturerProfile)a[0]!)),
                "GetLecturerProfileByCodeAsync" => Task.FromResult(lecturerProfiles.FirstOrDefault(l => l.LecturerCode == (string)a[0]!)),
                _ => null!
            };
            return res;
        });

        var userRepo = Stub<IUserRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetByIdAsync" => Task.FromResult(new User { Id = (Guid)a[0]!, FullName = "Nguyễn Văn A", Email = "a@fpt.edu.vn" }),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_Profiles" => profileRepo,
                "get_Users" => userRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var current = MockUser(UserRole.Admin, Guid.NewGuid());
        var service = new ProfileService(uow, current);

        var student = await service.CreateStudentProfileAsync(new CreateStudentProfileRequest
        {
            StudentCode = "SE180001",
            UserId = userId1,
            EnrollmentYear = 2022
        });

        Assert.Equal("SE180001", student.StudentCode);

        var lecturer = await service.CreateLecturerProfileAsync(new CreateLecturerProfileRequest
        {
            LecturerCode = "GV001",
            UserId = userId2,
            AcademicTitle = "Thạc sĩ"
        });

        Assert.Equal("GV001", lecturer.LecturerCode);
        Assert.Equal("Thạc sĩ", lecturer.AcademicTitle);
    }

    [Fact]
    public async Task OralPracticeService_StudentCanSubmitOwnAnswer()
    {
        var examId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        const string studentCode = "SE170123";

        var session = new StudentOralSession
        {
            Id = sessionId,
            OralExamId = examId,
            StudentCode = studentCode,
            StartTime = DateTime.UtcNow
        };

        var exam = new OralExam
        {
            Id = examId,
            ClassId = Guid.NewGuid(),
            Title = "Vấn đáp Đồ án"
        };

        var question = new OralQuestion
        {
            Id = questionId,
            OralExamId = examId,
            Content = "RESTful API là gì?",
            ExpectedKeywords = "HTTP, stateless, resource"
        };

        StudentOralAnswer? savedAnswer = null;

        var oralRepo = Stub<IOralPracticeRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetSessionByIdAsync" => Task.FromResult<StudentOralSession?>(session),
                "GetOralExamByIdAsync" => Task.FromResult<OralExam?>(exam),
                "GetQuestionByIdAsync" => Task.FromResult<OralQuestion?>(question),
                "GetAnswerBySessionAndQuestionAsync" => Task.FromResult(savedAnswer),
                "AddAnswerAsync" => Task.Run(() => savedAnswer = (StudentOralAnswer)a[0]!),
                _ => null!
            };
            return res;
        });

        var classRepo = Stub<IClassRepository>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "GetStudentCodeByUserIdAsync" => Task.FromResult<string?>(studentCode),
                _ => null!
            };
            return res;
        });

        var uow = Stub<IUnitOfWork>.Create((m, a) =>
        {
            object res = m.Name switch
            {
                "get_OralPractices" => oralRepo,
                "get_Classes" => classRepo,
                "SaveChangesAsync" => Task.FromResult(1),
                _ => null!
            };
            return res;
        });

        var current = MockUser(UserRole.Student, studentId);
        var service = new OralPracticeService(uow, current);

        var answer = await service.SubmitAnswerAsync(sessionId, new SubmitOralAnswerRequest
        {
            QuestionId = questionId,
            StudentAnswer = "RESTful là phong cách kiến trúc phần mềm dựa trên chuẩn HTTP."
        });

        Assert.NotNull(answer);
        Assert.Equal("RESTful là phong cách kiến trúc phần mềm dựa trên chuẩn HTTP.", answer.StudentAnswer);
        Assert.Null(answer.Feedback); // Sinh viên không được tự cấp feedback
    }
}
