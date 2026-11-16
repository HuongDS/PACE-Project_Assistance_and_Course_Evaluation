using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface ITimetableRepository
{
    // Rooms
    Task<List<RoomDto>> GetAllRoomsAsync(CancellationToken ct = default);
    Task<Room?> GetRoomByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> RoomCodeExistsAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task AddRoomAsync(Room room, CancellationToken ct = default);
    void UpdateRoom(Room room);
    void RemoveRoom(Room room);

    // TimeSlots
    Task<List<TimeSlotDto>> GetAllTimeSlotsAsync(CancellationToken ct = default);
    Task<TimeSlot?> GetTimeSlotByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> TimeSlotPeriodExistsAsync(int periodNumber, Guid? excludeId = null, CancellationToken ct = default);
    Task AddTimeSlotAsync(TimeSlot timeSlot, CancellationToken ct = default);
    void UpdateTimeSlot(TimeSlot timeSlot);
    void RemoveTimeSlot(TimeSlot timeSlot);

    // ClassSchedules
    Task<PagedResult<ClassScheduleDto>> SearchSchedulesAsync(ClassScheduleQuery query, CancellationToken ct = default);
    Task<ClassSchedule?> GetScheduleByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClassScheduleDto?> GetScheduleDetailAsync(Guid id, CancellationToken ct = default);
    Task AddScheduleAsync(ClassSchedule schedule, CancellationToken ct = default);
    void UpdateSchedule(ClassSchedule schedule);
    void RemoveSchedule(ClassSchedule schedule);

    // ClassSessions
    Task<PagedResult<ClassSessionDto>> SearchSessionsAsync(ClassSessionQuery query, CancellationToken ct = default);
    Task<ClassSession?> GetSessionByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClassSessionDto?> GetSessionDetailAsync(Guid id, CancellationToken ct = default);
    Task AddSessionAsync(ClassSession session, CancellationToken ct = default);
    void UpdateSession(ClassSession session);
    void RemoveSession(ClassSession session);
}
