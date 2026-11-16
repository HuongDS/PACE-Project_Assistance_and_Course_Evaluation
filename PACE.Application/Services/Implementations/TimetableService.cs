using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class TimetableService(
    IUnitOfWork uow,
    ICurrentUserService currentUser) : ITimetableService
{
    private void EnsureAdmin()
    {
        if (!currentUser.IsAdmin)
            throw new ForbiddenException("Chỉ quản trị viên mới có quyền thực hiện thao tác này.");
    }

    private async Task EnsureStaffAccessAsync(Guid classId, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        throw new ForbiddenException("Bạn không có quyền quản lý lịch của lớp học này.");
    }

    // Rooms
    public async Task<List<RoomDto>> GetAllRoomsAsync(CancellationToken ct = default)
    {
        return await uow.Timetables.GetAllRoomsAsync(ct);
    }

    public async Task<RoomDto> GetRoomByIdAsync(Guid id, CancellationToken ct = default)
    {
        var room = await uow.Timetables.GetRoomByIdAsync(id, ct)
            ?? throw new NotFoundException("Phòng học không tồn tại.");
        return new RoomDto(room.Id, room.Code, room.Building);
    }

    public async Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();

        var trimmedCode = request.Code.Trim();
        var exists = await uow.Timetables.RoomCodeExistsAsync(trimmedCode, null, ct);
        if (exists)
            throw new ConflictException($"Mã phòng '{trimmedCode}' đã tồn tại trong hệ thống.");

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Code = trimmedCode,
            Building = request.Building?.Trim()
        };

        await uow.Timetables.AddRoomAsync(room, ct);
        await uow.SaveChangesAsync(ct);

        return new RoomDto(room.Id, room.Code, room.Building);
    }

    public async Task<RoomDto> UpdateRoomAsync(Guid id, UpdateRoomRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();

        var room = await uow.Timetables.GetRoomByIdAsync(id, ct)
            ?? throw new NotFoundException("Phòng học không tồn tại.");

        var trimmedCode = request.Code.Trim();
        var duplicate = await uow.Timetables.RoomCodeExistsAsync(trimmedCode, id, ct);
        if (duplicate)
            throw new ConflictException($"Mã phòng '{trimmedCode}' đã tồn tại trong hệ thống.");

        room.Code = trimmedCode;
        room.Building = request.Building?.Trim();

        uow.Timetables.UpdateRoom(room);
        await uow.SaveChangesAsync(ct);

        return new RoomDto(room.Id, room.Code, room.Building);
    }

    public async Task DeleteRoomAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAdmin();

        var room = await uow.Timetables.GetRoomByIdAsync(id, ct)
            ?? throw new NotFoundException("Phòng học không tồn tại.");

        uow.Timetables.RemoveRoom(room);
        await uow.SaveChangesAsync(ct);
    }

    // TimeSlots
    public async Task<List<TimeSlotDto>> GetAllTimeSlotsAsync(CancellationToken ct = default)
    {
        return await uow.Timetables.GetAllTimeSlotsAsync(ct);
    }

    public async Task<TimeSlotDto> GetTimeSlotByIdAsync(Guid id, CancellationToken ct = default)
    {
        var slot = await uow.Timetables.GetTimeSlotByIdAsync(id, ct)
            ?? throw new NotFoundException("Tiết học không tồn tại.");
        return new TimeSlotDto(slot.Id, slot.PeriodNumber, slot.StartTime, slot.EndTime);
    }

    public async Task<TimeSlotDto> CreateTimeSlotAsync(CreateTimeSlotRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();

        if (request.StartTime >= request.EndTime)
            throw new BadRequestException("Giờ bắt đầu tiết học phải trước giờ kết thúc.");

        var exists = await uow.Timetables.TimeSlotPeriodExistsAsync(request.PeriodNumber, null, ct);
        if (exists)
            throw new ConflictException($"Tiết số {request.PeriodNumber} đã tồn tại trong hệ thống.");

        var slot = new TimeSlot
        {
            Id = Guid.NewGuid(),
            PeriodNumber = request.PeriodNumber,
            StartTime = request.StartTime,
            EndTime = request.EndTime
        };

        await uow.Timetables.AddTimeSlotAsync(slot, ct);
        await uow.SaveChangesAsync(ct);

        return new TimeSlotDto(slot.Id, slot.PeriodNumber, slot.StartTime, slot.EndTime);
    }

    public async Task<TimeSlotDto> UpdateTimeSlotAsync(Guid id, UpdateTimeSlotRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();

        var slot = await uow.Timetables.GetTimeSlotByIdAsync(id, ct)
            ?? throw new NotFoundException("Tiết học không tồn tại.");

        if (request.StartTime >= request.EndTime)
            throw new BadRequestException("Giờ bắt đầu tiết học phải trước giờ kết thúc.");

        var duplicate = await uow.Timetables.TimeSlotPeriodExistsAsync(request.PeriodNumber, id, ct);
        if (duplicate)
            throw new ConflictException($"Tiết số {request.PeriodNumber} đã tồn tại trong hệ thống.");

        slot.PeriodNumber = request.PeriodNumber;
        slot.StartTime = request.StartTime;
        slot.EndTime = request.EndTime;

        uow.Timetables.UpdateTimeSlot(slot);
        await uow.SaveChangesAsync(ct);

        return new TimeSlotDto(slot.Id, slot.PeriodNumber, slot.StartTime, slot.EndTime);
    }

    public async Task DeleteTimeSlotAsync(Guid id, CancellationToken ct = default)
    {
        EnsureAdmin();

        var slot = await uow.Timetables.GetTimeSlotByIdAsync(id, ct)
            ?? throw new NotFoundException("Tiết học không tồn tại.");

        uow.Timetables.RemoveTimeSlot(slot);
        await uow.SaveChangesAsync(ct);
    }

    // ClassSchedules
    public async Task<PagedResult<ClassScheduleDto>> GetSchedulesPagedAsync(ClassScheduleQuery query, CancellationToken ct = default)
    {
        return await uow.Timetables.SearchSchedulesAsync(query, ct);
    }

    public async Task<ClassScheduleDto> GetScheduleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var schedule = await uow.Timetables.GetScheduleDetailAsync(id, ct)
            ?? throw new NotFoundException("Lịch học không tồn tại.");
        return schedule;
    }

    public async Task<ClassScheduleDto> CreateScheduleAsync(CreateClassScheduleRequest request, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(request.ClassId, ct);

        if (request.ValidFrom >= request.ValidTo)
            throw new BadRequestException("Ngày bắt đầu áp dụng lịch (ValidFrom) phải trước ngày kết thúc (ValidTo).");

        var classExists = await uow.Classes.ClassExistsAsync(request.ClassId, ct);
        if (!classExists)
            throw new NotFoundException("Lớp học không tồn tại.");

        var lecturer = await uow.Profiles.GetLecturerProfileByCodeAsync(request.LecturerCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy giảng viên với mã '{request.LecturerCode}'.");

        var room = await uow.Timetables.GetRoomByIdAsync(request.RoomId, ct)
            ?? throw new NotFoundException("Phòng học không tồn tại.");

        var slot = await uow.Timetables.GetTimeSlotByIdAsync(request.TimeSlotId, ct)
            ?? throw new NotFoundException("Tiết học không tồn tại.");

        var schedule = new ClassSchedule
        {
            Id = Guid.NewGuid(),
            ClassId = request.ClassId,
            LecturerCode = lecturer.LecturerCode,
            RoomId = room.Id,
            TimeSlotId = slot.Id,
            DayOfWeek = request.DayOfWeek,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        await uow.Timetables.AddScheduleAsync(schedule, ct);
        await uow.SaveChangesAsync(ct);

        return (await uow.Timetables.GetScheduleDetailAsync(schedule.Id, ct))!;
    }

    public async Task<ClassScheduleDto> UpdateScheduleAsync(Guid id, UpdateClassScheduleRequest request, CancellationToken ct = default)
    {
        var schedule = await uow.Timetables.GetScheduleByIdAsync(id, ct)
            ?? throw new NotFoundException("Lịch học không tồn tại.");

        await EnsureStaffAccessAsync(schedule.ClassId, ct);

        if (request.ValidFrom >= request.ValidTo)
            throw new BadRequestException("Ngày bắt đầu áp dụng lịch (ValidFrom) phải trước ngày kết thúc (ValidTo).");

        var lecturer = await uow.Profiles.GetLecturerProfileByCodeAsync(request.LecturerCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy giảng viên với mã '{request.LecturerCode}'.");

        var room = await uow.Timetables.GetRoomByIdAsync(request.RoomId, ct)
            ?? throw new NotFoundException("Phòng học không tồn tại.");

        var slot = await uow.Timetables.GetTimeSlotByIdAsync(request.TimeSlotId, ct)
            ?? throw new NotFoundException("Tiết học không tồn tại.");

        schedule.LecturerCode = lecturer.LecturerCode;
        schedule.RoomId = room.Id;
        schedule.TimeSlotId = slot.Id;
        schedule.DayOfWeek = request.DayOfWeek;
        schedule.ValidFrom = request.ValidFrom;
        schedule.ValidTo = request.ValidTo;

        uow.Timetables.UpdateSchedule(schedule);
        await uow.SaveChangesAsync(ct);

        return (await uow.Timetables.GetScheduleDetailAsync(schedule.Id, ct))!;
    }

    public async Task DeleteScheduleAsync(Guid id, CancellationToken ct = default)
    {
        var schedule = await uow.Timetables.GetScheduleByIdAsync(id, ct)
            ?? throw new NotFoundException("Lịch học không tồn tại.");

        await EnsureStaffAccessAsync(schedule.ClassId, ct);

        uow.Timetables.RemoveSchedule(schedule);
        await uow.SaveChangesAsync(ct);
    }

    // ClassSessions
    public async Task<PagedResult<ClassSessionDto>> GetSessionsPagedAsync(ClassSessionQuery query, CancellationToken ct = default)
    {
        return await uow.Timetables.SearchSessionsAsync(query, ct);
    }

    public async Task<ClassSessionDto> GetSessionByIdAsync(Guid id, CancellationToken ct = default)
    {
        var session = await uow.Timetables.GetSessionDetailAsync(id, ct)
            ?? throw new NotFoundException("Buổi học không tồn tại.");
        return session;
    }

    public async Task<ClassSessionDto> CreateSessionAsync(CreateClassSessionRequest request, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(request.ClassId, ct);

        var classExists = await uow.Classes.ClassExistsAsync(request.ClassId, ct);
        if (!classExists)
            throw new NotFoundException("Lớp học không tồn tại.");

        var lecturer = await uow.Profiles.GetLecturerProfileByCodeAsync(request.LecturerCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy giảng viên với mã '{request.LecturerCode}'.");

        var room = await uow.Timetables.GetRoomByIdAsync(request.RoomId, ct)
            ?? throw new NotFoundException("Phòng học không tồn tại.");

        var slot = await uow.Timetables.GetTimeSlotByIdAsync(request.TimeSlotId, ct)
            ?? throw new NotFoundException("Tiết học không tồn tại.");

        var session = new ClassSession
        {
            Id = Guid.NewGuid(),
            ClassId = request.ClassId,
            ScheduleId = request.ScheduleId,
            LearnDate = request.LearnDate,
            TimeSlotId = slot.Id,
            RoomId = room.Id,
            LecturerCode = lecturer.LecturerCode,
            IsCancelled = request.IsCancelled,
            Note = request.Note?.Trim()
        };

        await uow.Timetables.AddSessionAsync(session, ct);
        await uow.SaveChangesAsync(ct);

        return (await uow.Timetables.GetSessionDetailAsync(session.Id, ct))!;
    }

    public async Task<ClassSessionDto> UpdateSessionAsync(Guid id, UpdateClassSessionRequest request, CancellationToken ct = default)
    {
        var session = await uow.Timetables.GetSessionByIdAsync(id, ct)
            ?? throw new NotFoundException("Buổi học không tồn tại.");

        await EnsureStaffAccessAsync(session.ClassId, ct);

        var lecturer = await uow.Profiles.GetLecturerProfileByCodeAsync(request.LecturerCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy giảng viên với mã '{request.LecturerCode}'.");

        var room = await uow.Timetables.GetRoomByIdAsync(request.RoomId, ct)
            ?? throw new NotFoundException("Phòng học không tồn tại.");

        var slot = await uow.Timetables.GetTimeSlotByIdAsync(request.TimeSlotId, ct)
            ?? throw new NotFoundException("Tiết học không tồn tại.");

        session.LearnDate = request.LearnDate;
        session.TimeSlotId = slot.Id;
        session.RoomId = room.Id;
        session.LecturerCode = lecturer.LecturerCode;
        session.IsCancelled = request.IsCancelled;
        session.Note = request.Note?.Trim();

        uow.Timetables.UpdateSession(session);
        await uow.SaveChangesAsync(ct);

        return (await uow.Timetables.GetSessionDetailAsync(session.Id, ct))!;
    }

    public async Task DeleteSessionAsync(Guid id, CancellationToken ct = default)
    {
        var session = await uow.Timetables.GetSessionByIdAsync(id, ct)
            ?? throw new NotFoundException("Buổi học không tồn tại.");

        await EnsureStaffAccessAsync(session.ClassId, ct);

        uow.Timetables.RemoveSession(session);
        await uow.SaveChangesAsync(ct);
    }
}
