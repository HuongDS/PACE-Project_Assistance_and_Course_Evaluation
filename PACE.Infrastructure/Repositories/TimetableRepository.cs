using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class TimetableRepository(AppDbContext context) : ITimetableRepository
{
    private static string? NormalizeSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();

    // Rooms
    public async Task<List<RoomDto>> GetAllRoomsAsync(CancellationToken ct = default)
    {
        return await context.Rooms.AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new RoomDto(r.Id, r.Code, r.Building))
            .ToListAsync(ct);
    }

    public async Task<Room?> GetRoomByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Rooms.FindAsync([id], ct);
    }

    public async Task<bool> RoomCodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToLower();
        return await context.Rooms.AnyAsync(r => (!excludeId.HasValue || r.Id != excludeId.Value) && r.Code.ToLower() == normalized, ct);
    }

    public async Task AddRoomAsync(Room room, CancellationToken ct = default)
    {
        await context.Rooms.AddAsync(room, ct);
    }

    public void UpdateRoom(Room room)
    {
        context.Rooms.Update(room);
    }

    public void RemoveRoom(Room room)
    {
        context.Rooms.Remove(room);
    }

    // TimeSlots
    public async Task<List<TimeSlotDto>> GetAllTimeSlotsAsync(CancellationToken ct = default)
    {
        return await context.TimeSlots.AsNoTracking()
            .OrderBy(t => t.PeriodNumber)
            .Select(t => new TimeSlotDto(t.Id, t.PeriodNumber, t.StartTime, t.EndTime))
            .ToListAsync(ct);
    }

    public async Task<TimeSlot?> GetTimeSlotByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.TimeSlots.FindAsync([id], ct);
    }

    public async Task<bool> TimeSlotPeriodExistsAsync(int periodNumber, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await context.TimeSlots.AnyAsync(t => (!excludeId.HasValue || t.Id != excludeId.Value) && t.PeriodNumber == periodNumber, ct);
    }

    public async Task AddTimeSlotAsync(TimeSlot timeSlot, CancellationToken ct = default)
    {
        await context.TimeSlots.AddAsync(timeSlot, ct);
    }

    public void UpdateTimeSlot(TimeSlot timeSlot)
    {
        context.TimeSlots.Update(timeSlot);
    }

    public void RemoveTimeSlot(TimeSlot timeSlot)
    {
        context.TimeSlots.Remove(timeSlot);
    }

    // ClassSchedules
    public async Task<PagedResult<ClassScheduleDto>> SearchSchedulesAsync(ClassScheduleQuery query, CancellationToken ct = default)
    {
        var q = context.ClassSchedules.AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.LecturerProfile).ThenInclude(l => l.User)
            .Include(s => s.Room)
            .Include(s => s.TimeSlot)
            .AsQueryable();

        if (query.ClassId.HasValue) q = q.Where(s => s.ClassId == query.ClassId.Value);
        if (!string.IsNullOrEmpty(query.LecturerCode)) q = q.Where(s => s.LecturerCode == query.LecturerCode);
        if (query.RoomId.HasValue) q = q.Where(s => s.RoomId == query.RoomId.Value);
        if (query.DayOfWeek.HasValue) q = q.Where(s => s.DayOfWeek == query.DayOfWeek.Value);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.TimeSlot.PeriodNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ClassScheduleDto(
                s.Id,
                s.ClassId,
                s.Class.Name,
                s.LecturerCode,
                s.LecturerProfile.User.FullName,
                s.RoomId,
                s.Room.Code,
                s.TimeSlotId,
                s.TimeSlot.PeriodNumber,
                s.TimeSlot.StartTime,
                s.TimeSlot.EndTime,
                s.DayOfWeek,
                s.ValidFrom,
                s.ValidTo))
            .ToListAsync(ct);

        return new PagedResult<ClassScheduleDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<ClassSchedule?> GetScheduleByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.ClassSchedules.FindAsync([id], ct);
    }

    public async Task<ClassScheduleDto?> GetScheduleDetailAsync(Guid id, CancellationToken ct = default)
    {
        return await context.ClassSchedules.AsNoTracking()
            .Where(s => s.Id == id)
            .Include(s => s.Class)
            .Include(s => s.LecturerProfile).ThenInclude(l => l.User)
            .Include(s => s.Room)
            .Include(s => s.TimeSlot)
            .Select(s => new ClassScheduleDto(
                s.Id,
                s.ClassId,
                s.Class.Name,
                s.LecturerCode,
                s.LecturerProfile.User.FullName,
                s.RoomId,
                s.Room.Code,
                s.TimeSlotId,
                s.TimeSlot.PeriodNumber,
                s.TimeSlot.StartTime,
                s.TimeSlot.EndTime,
                s.DayOfWeek,
                s.ValidFrom,
                s.ValidTo))
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddScheduleAsync(ClassSchedule schedule, CancellationToken ct = default)
    {
        await context.ClassSchedules.AddAsync(schedule, ct);
    }

    public void UpdateSchedule(ClassSchedule schedule)
    {
        context.ClassSchedules.Update(schedule);
    }

    public void RemoveSchedule(ClassSchedule schedule)
    {
        context.ClassSchedules.Remove(schedule);
    }

    // ClassSessions
    public async Task<PagedResult<ClassSessionDto>> SearchSessionsAsync(ClassSessionQuery query, CancellationToken ct = default)
    {
        var q = context.ClassSessions.AsNoTracking()
            .Include(s => s.Class)
            .Include(s => s.LecturerProfile).ThenInclude(l => l.User)
            .Include(s => s.Room)
            .Include(s => s.TimeSlot)
            .AsQueryable();

        if (query.ClassId.HasValue) q = q.Where(s => s.ClassId == query.ClassId.Value);
        if (query.LearnDate.HasValue) q = q.Where(s => s.LearnDate == query.LearnDate.Value);
        if (query.FromDate.HasValue) q = q.Where(s => s.LearnDate >= query.FromDate.Value);
        if (query.ToDate.HasValue) q = q.Where(s => s.LearnDate <= query.ToDate.Value);
        if (!string.IsNullOrEmpty(query.LecturerCode)) q = q.Where(s => s.LecturerCode == query.LecturerCode);
        if (query.RoomId.HasValue) q = q.Where(s => s.RoomId == query.RoomId.Value);
        if (query.IsCancelled.HasValue) q = q.Where(s => s.IsCancelled == query.IsCancelled.Value);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(s => s.LearnDate).ThenBy(s => s.TimeSlot.PeriodNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ClassSessionDto(
                s.Id,
                s.ClassId,
                s.Class.Name,
                s.ScheduleId,
                s.LearnDate,
                s.TimeSlotId,
                s.TimeSlot.PeriodNumber,
                s.TimeSlot.StartTime,
                s.TimeSlot.EndTime,
                s.RoomId,
                s.Room.Code,
                s.LecturerCode,
                s.LecturerProfile.User.FullName,
                s.IsCancelled,
                s.Note))
            .ToListAsync(ct);

        return new PagedResult<ClassSessionDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<ClassSession?> GetSessionByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.ClassSessions.FindAsync([id], ct);
    }

    public async Task<ClassSessionDto?> GetSessionDetailAsync(Guid id, CancellationToken ct = default)
    {
        return await context.ClassSessions.AsNoTracking()
            .Where(s => s.Id == id)
            .Include(s => s.Class)
            .Include(s => s.LecturerProfile).ThenInclude(l => l.User)
            .Include(s => s.Room)
            .Include(s => s.TimeSlot)
            .Select(s => new ClassSessionDto(
                s.Id,
                s.ClassId,
                s.Class.Name,
                s.ScheduleId,
                s.LearnDate,
                s.TimeSlotId,
                s.TimeSlot.PeriodNumber,
                s.TimeSlot.StartTime,
                s.TimeSlot.EndTime,
                s.RoomId,
                s.Room.Code,
                s.LecturerCode,
                s.LecturerProfile.User.FullName,
                s.IsCancelled,
                s.Note))
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddSessionAsync(ClassSession session, CancellationToken ct = default)
    {
        await context.ClassSessions.AddAsync(session, ct);
    }

    public void UpdateSession(ClassSession session)
    {
        context.ClassSessions.Update(session);
    }

    public void RemoveSession(ClassSession session)
    {
        context.ClassSessions.Remove(session);
    }
}
