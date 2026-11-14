using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class ClassService(
    IUnitOfWork uow,
    ICurrentUserService currentUser,
    IClassExcelService classExcelService) : IClassService
{
    private async Task EnsureStaffAccessAsync(Guid classId, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        throw new ForbiddenException("Bạn không có quyền quản lý sinh viên của lớp này.");
    }

    public async Task<PagedResult<ClassDto>> GetPagedAsync(ClassQuery query, CancellationToken ct = default)
    {
        if (currentUser.IsAdmin)
        {
            return await uow.Classes.SearchClassesAsync(query, null, null, ct);
        }

        if (currentUser.Role == UserRole.Lecturer)
        {
            return await uow.Classes.SearchClassesAsync(query, currentUser.UserId, null, ct);
        }

        // Role == Student
        var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
        if (string.IsNullOrEmpty(studentCode))
        {
            return new PagedResult<ClassDto>
            {
                Items = [],
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = 0
            };
        }

        return await uow.Classes.SearchClassesAsync(query, null, studentCode, ct);
    }

    public async Task<ClassDetailDto> GetDetailAsync(Guid classId, CancellationToken ct = default)
    {
        var detail = await uow.Classes.GetClassDetailAsync(classId, ct)
                     ?? throw new NotFoundException("Lớp học không tồn tại.");

        if (currentUser.IsAdmin) return detail;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
            if (!isLecturer)
                throw new ForbiddenException("Bạn không phụ trách lớp học này.");
            return detail;
        }

        // Student
        var isStudent = await uow.Classes.IsStudentOfClassAsync(classId, currentUser.UserId, ct: ct);
        if (!isStudent)
            throw new ForbiddenException("Bạn không thuộc danh sách sinh viên của lớp này.");

        return detail;
    }

    public async Task<PagedResult<ClassStudentDto>> GetStudentsAsync(Guid classId, ClassStudentQuery query, CancellationToken ct = default)
    {
        if (!await uow.Classes.ClassExistsAsync(classId, ct))
            throw new NotFoundException("Lớp học không tồn tại.");

        if (!currentUser.IsAdmin)
        {
            if (currentUser.Role == UserRole.Lecturer)
            {
                var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
                if (!isLecturer)
                    throw new ForbiddenException("Bạn không phụ trách lớp học này.");
            }
            else
            {
                var isStudent = await uow.Classes.IsStudentOfClassAsync(classId, currentUser.UserId, ct: ct);
                if (!isStudent)
                    throw new ForbiddenException("Bạn không thuộc danh sách sinh viên của lớp này.");
            }
        }

        return await uow.Classes.GetClassStudentsAsync(classId, query, ct);
    }

    public async Task<int> AddStudentsAsync(Guid classId, AddStudentsToClassRequest request, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(classId, ct);

        if (!await uow.Classes.ClassExistsAsync(classId, ct))
            throw new NotFoundException("Lớp học không tồn tại.");

        if (request.StudentCodes == null || request.StudentCodes.Count == 0)
            throw new BadRequestException("Danh sách mã sinh viên không được rỗng.");

        return await uow.Classes.AddStudentsToClassAsync(classId, request.StudentCodes, ct);
    }

    public async Task RemoveStudentAsync(Guid classId, string studentCode, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(classId, ct);

        if (!await uow.Classes.ClassExistsAsync(classId, ct))
            throw new NotFoundException("Lớp học không tồn tại.");

        var removed = await uow.Classes.RemoveStudentFromClassAsync(classId, studentCode, ct);
        if (!removed)
            throw new NotFoundException("Sinh viên không thuộc lớp này.");
    }

    public async Task<ImportClassStudentsResult> ImportStudentsAsync(Guid classId, Stream excelStream, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(classId, ct);

        if (!await uow.Classes.ClassExistsAsync(classId, ct))
            throw new NotFoundException("Lớp học không tồn tại.");

        var rows = classExcelService.ReadStudentList(excelStream);
        if (rows.Count == 0)
            throw new BadRequestException("File Excel không có dữ liệu sinh viên.");

        int successCount = 0;
        int alreadyInClass = 0;
        int newUsersCreated = 0;
        var errors = new List<string>();

        foreach (var row in rows)
        {
            var code = row.StudentCode.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) continue;

            try
            {
                // Ensure user exists
                var user = await uow.Users.GetByStudentCodeAsync(code, ct);
                if (user == null)
                {
                    var email = !string.IsNullOrEmpty(row.Email)
                        ? row.Email.Trim().ToLowerInvariant()
                        : $"{code.ToLowerInvariant()}@fpt.edu.vn";

                    user = await uow.Users.GetByEmailAsync(email, ct);
                    if (user == null)
                    {
                        user = new User
                        {
                            Id = Guid.NewGuid(),
                            StudentCode = code,
                            Email = email,
                            FullName = !string.IsNullOrEmpty(row.FullName) ? row.FullName.Trim() : code,
                            Role = UserRole.Student,
                            CreatedAt = DateTime.UtcNow
                        };
                        await uow.Users.AddAsync(user, ct);
                        await uow.SaveChangesAsync(ct);
                        newUsersCreated++;
                    }
                    else if (string.IsNullOrEmpty(user.StudentCode))
                    {
                        user.StudentCode = code;
                        if (!string.IsNullOrEmpty(row.FullName) && string.IsNullOrEmpty(user.FullName))
                            user.FullName = row.FullName.Trim();
                        await uow.SaveChangesAsync(ct);
                    }
                }
                else if (!string.IsNullOrEmpty(row.FullName) && string.IsNullOrEmpty(user.FullName))
                {
                    user.FullName = row.FullName.Trim();
                    await uow.SaveChangesAsync(ct);
                }

                // Add to class
                var added = await uow.Classes.AddStudentsToClassAsync(classId, [code], ct);
                if (added > 0) successCount++;
                else alreadyInClass++;
            }
            catch (Exception ex)
            {
                errors.Add($"Dòng {row.Row} ({code}): {ex.Message}");
            }
        }

        return new ImportClassStudentsResult(rows.Count, successCount, alreadyInClass, newUsersCreated, errors);
    }

    public async Task<(byte[] FileBytes, string FileName)> ExportStudentsAsync(Guid classId, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(classId, ct);

        var detail = await uow.Classes.GetClassDetailAsync(classId, ct)
                     ?? throw new NotFoundException("Lớp học không tồn tại.");

        var students = await uow.Classes.GetAllStudentsInClassAsync(classId, ct);
        var className = detail.Class.Name ?? "Lop_hoc";

        var bytes = classExcelService.ExportStudentList(className, students);
        var fileName = $"Danh_sach_sinh_vien_{className}_{DateTime.UtcNow:yyyyMMdd_HHmm}.xlsx";

        return (bytes, fileName);
    }

    public async Task<ClassDto> CreateClassAsync(CreateClassRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin && currentUser.Role != UserRole.Lecturer)
            throw new ForbiddenException("Bạn không có quyền tạo lớp học.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Tên lớp học không được để trống.");

        var trimmedName = request.Name.Trim();

        // 1. Kiểm tra môn học (Course)
        var course = await uow.Courses.GetByIdWithLecturerAsync(request.CourseId, ct)
            ?? throw new NotFoundException("Môn học không tồn tại.");

        if (!currentUser.IsAdmin && course.LecturerId != currentUser.UserId)
            throw new ForbiddenException("Bạn chỉ được tạo lớp cho môn học do mình phụ trách.");

        // 2. Xác định học kỳ (Semester)
        var currentSemester = await uow.Semesters.GetCurrentSemesterAsync(ct)
            ?? throw new BadRequestException("Hệ thống chưa có học kỳ nào được thiết lập. Vui lòng liên hệ quản trị viên.");

        Semester targetSemester;
        if (currentUser.IsAdmin)
        {
            if (request.SemesterId.HasValue && request.SemesterId.Value != currentSemester.Id)
            {
                targetSemester = await uow.Semesters.GetByIdAsync(request.SemesterId.Value, ct)
                    ?? throw new NotFoundException("Học kỳ không tồn tại.");
            }
            else
            {
                targetSemester = currentSemester;
            }
        }
        else
        {
            // Giảng viên chỉ được tạo lớp trong học kỳ hiện tại
            if (request.SemesterId.HasValue && request.SemesterId.Value != currentSemester.Id)
            {
                throw new BadRequestException($"Giảng viên chỉ được phép tạo lớp học trong học kỳ hiện tại ({currentSemester.Name}).");
            }
            targetSemester = currentSemester;
        }

        // 3. Kiểm tra trùng tên lớp trong học kỳ
        var exists = await uow.Classes.AnyAsync(
            c => c.SemesterId == targetSemester.Id && c.Name != null && c.Name.ToLower() == trimmedName.ToLower(), ct);
        if (exists)
            throw new ConflictException($"Lớp học '{trimmedName}' đã tồn tại trong học kỳ {targetSemester.Name}.");

        // 4. Ngày bắt đầu / kết thúc
        var startDate = request.StartDate ?? targetSemester.StartDate;
        var actualStartDate = request.ActualStartDate ?? startDate;
        var endDate = request.EndDate ?? targetSemester.EndDate;

        if (startDate >= endDate)
            throw new BadRequestException("Ngày bắt đầu lớp học phải trước ngày kết thúc.");

        var newClass = new Class
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            CourseId = course.Id,
            SemesterId = targetSemester.Id,
            StartDate = startDate,
            ActualStartDate = actualStartDate,
            EndDate = endDate
        };

        await uow.Classes.AddAsync(newClass, ct);
        await uow.SaveChangesAsync(ct);

        return new ClassDto(
            newClass.Id,
            newClass.Name,
            course.Id,
            course.Code,
            course.Name,
            course.LecturerId,
            course.Lecturer?.FullName ?? "",
            course.Lecturer?.Email ?? "",
            targetSemester.Name,
            newClass.StartDate,
            newClass.EndDate,
            0,
            0);
    }

    public async Task<ImportClassesResult> ImportClassesAsync(Stream excelStream, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin && currentUser.Role != UserRole.Lecturer)
            throw new ForbiddenException("Bạn không có quyền import danh sách lớp học.");

        var currentSemester = await uow.Semesters.GetCurrentSemesterAsync(ct)
            ?? throw new BadRequestException("Hệ thống chưa có học kỳ hiện tại nào. Vui lòng liên hệ quản trị viên.");

        var rows = classExcelService.ReadClassList(excelStream);
        if (rows.Count == 0)
            throw new BadRequestException("File Excel không có dữ liệu lớp học.");

        List<Course> availableCourses;
        if (currentUser.IsAdmin)
        {
            availableCourses = await uow.Courses.GetAllWithLecturerAsync(ct);
        }
        else
        {
            availableCourses = await uow.Courses.GetByLecturerIdAsync(currentUser.UserId, ct);
        }

        var courseMap = availableCourses
            .GroupBy(c => c.Code.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        int successCount = 0;
        int skippedCount = 0;
        var errors = new List<string>();
        var createdDtos = new List<ClassDto>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var className = row.ClassName?.Trim() ?? "";
            var courseCode = row.CourseCode?.Trim() ?? "";

            if (string.IsNullOrEmpty(className))
            {
                errors.Add($"Dòng {row.Row}: Tên lớp học không được để trống.");
                skippedCount++;
                continue;
            }

            if (string.IsNullOrEmpty(courseCode))
            {
                errors.Add($"Dòng {row.Row} ({className}): Mã môn học không được để trống.");
                skippedCount++;
                continue;
            }

            if (seenNames.Contains(className))
            {
                errors.Add($"Dòng {row.Row} ({className}): Bị trùng lặp tên lớp ngay trong file Excel.");
                skippedCount++;
                continue;
            }

            if (!courseMap.TryGetValue(courseCode.ToLowerInvariant(), out var course))
            {
                if (currentUser.Role == UserRole.Lecturer)
                {
                    errors.Add($"Dòng {row.Row} ({className}): Môn học '{courseCode}' không tồn tại hoặc không do bạn phụ trách.");
                }
                else
                {
                    errors.Add($"Dòng {row.Row} ({className}): Mã môn học '{courseCode}' không tồn tại trong hệ thống.");
                }
                skippedCount++;
                continue;
            }

            var exists = await uow.Classes.AnyAsync(
                c => c.SemesterId == currentSemester.Id && c.Name != null && c.Name.ToLower() == className.ToLower(), ct);
            if (exists)
            {
                errors.Add($"Dòng {row.Row} ({className}): Lớp học đã tồn tại trong học kỳ {currentSemester.Name}.");
                skippedCount++;
                continue;
            }

            var startDate = row.StartDate ?? currentSemester.StartDate;
            var endDate = row.EndDate ?? currentSemester.EndDate;

            if (startDate >= endDate)
            {
                errors.Add($"Dòng {row.Row} ({className}): Ngày bắt đầu ({startDate:dd/MM/yyyy}) phải trước ngày kết thúc ({endDate:dd/MM/yyyy}).");
                skippedCount++;
                continue;
            }

            var newClass = new Class
            {
                Id = Guid.NewGuid(),
                Name = className,
                CourseId = course.Id,
                SemesterId = currentSemester.Id,
                StartDate = startDate,
                ActualStartDate = startDate,
                EndDate = endDate
            };

            await uow.Classes.AddAsync(newClass, ct);
            seenNames.Add(className);
            successCount++;

            createdDtos.Add(new ClassDto(
                newClass.Id,
                newClass.Name,
                course.Id,
                course.Code,
                course.Name,
                course.LecturerId,
                course.Lecturer?.FullName ?? "",
                course.Lecturer?.Email ?? "",
                currentSemester.Name,
                newClass.StartDate,
                newClass.EndDate,
                0,
                0));
        }

        if (successCount > 0)
        {
            await uow.SaveChangesAsync(ct);
        }

        return new ImportClassesResult(rows.Count, successCount, skippedCount, errors, createdDtos);
    }

    public Task<(byte[] FileBytes, string FileName)> ExportClassTemplateAsync(CancellationToken ct = default)
    {
        var bytes = classExcelService.ExportClassTemplate();
        var fileName = $"Template_Import_Lop_Hoc_{DateTime.UtcNow:yyyyMMdd}.xlsx";
        return Task.FromResult((bytes, fileName));
    }
}

