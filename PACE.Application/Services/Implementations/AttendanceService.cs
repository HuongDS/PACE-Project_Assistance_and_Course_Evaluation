using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class AttendanceService(
    IUnitOfWork uow,
    ICurrentUserService currentUser) : IAttendanceService
{
    private async Task EnsureStaffAccessAsync(Guid classId, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        throw new ForbiddenException("Bạn không có quyền điểm danh lớp học này.");
    }

    public async Task<PagedResult<AttendanceDto>> GetPagedAsync(AttendanceQuery query, CancellationToken ct = default)
    {
        return await uow.Attendances.SearchAttendanceAsync(query, ct);
    }

    public async Task<List<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await uow.Attendances.GetBySessionIdAsync(sessionId, ct);
    }

    public async Task<List<AttendanceDto>> BatchRecordAsync(Guid sessionId, BatchRecordAttendanceRequest request, CancellationToken ct = default)
    {
        var session = await uow.Timetables.GetSessionByIdAsync(sessionId, ct)
            ?? throw new NotFoundException("Buổi học không tồn tại.");

        await EnsureStaffAccessAsync(session.ClassId, ct);

        var now = DateTime.UtcNow;

        foreach (var item in request.Items)
        {
            var existing = await uow.Attendances.GetBySessionAndStudentAsync(sessionId, item.StudentCode, ct);
            if (existing != null)
            {
                existing.Status = item.Status;
                existing.RecordedAt = now;
                existing.Comments = item.Comments?.Trim();
                uow.Attendances.Update(existing);
            }
            else
            {
                var newAttendance = new Attendance
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    StudentCode = item.StudentCode.Trim(),
                    Status = item.Status,
                    RecordedAt = now,
                    Comments = item.Comments?.Trim()
                };
                await uow.Attendances.AddAsync(newAttendance, ct);
            }
        }

        await uow.SaveChangesAsync(ct);

        return await uow.Attendances.GetBySessionIdAsync(sessionId, ct);
    }

    public async Task<AttendanceDto> UpdateAsync(Guid id, UpdateAttendanceRequest request, CancellationToken ct = default)
    {
        var attendance = await uow.Attendances.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Bản ghi điểm danh không tồn tại.");

        var session = await uow.Timetables.GetSessionByIdAsync(attendance.SessionId, ct)
            ?? throw new NotFoundException("Buổi học không tồn tại.");

        await EnsureStaffAccessAsync(session.ClassId, ct);

        attendance.Status = request.Status;
        attendance.Comments = request.Comments?.Trim();
        attendance.RecordedAt = DateTime.UtcNow;

        uow.Attendances.Update(attendance);
        await uow.SaveChangesAsync(ct);

        var profile = await uow.Profiles.GetStudentProfileByCodeAsync(attendance.StudentCode, ct);

        return new AttendanceDto(
            attendance.Id,
            attendance.SessionId,
            attendance.StudentCode,
            profile?.UserId,
            profile?.User?.FullName,
            profile?.User?.Email,
            attendance.Status,
            attendance.RecordedAt,
            attendance.Comments);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var attendance = await uow.Attendances.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Bản ghi điểm danh không tồn tại.");

        var session = await uow.Timetables.GetSessionByIdAsync(attendance.SessionId, ct)
            ?? throw new NotFoundException("Buổi học không tồn tại.");

        await EnsureStaffAccessAsync(session.ClassId, ct);

        uow.Attendances.Remove(attendance);
        await uow.SaveChangesAsync(ct);
    }
}
