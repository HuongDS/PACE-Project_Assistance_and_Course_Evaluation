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
}

