using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface ITimetableService
{
    // Rooms
    Task<List<RoomDto>> GetAllRoomsAsync(CancellationToken ct = default);
    Task<RoomDto> GetRoomByIdAsync(Guid id, CancellationToken ct = default);
    Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default);
    Task<RoomDto> UpdateRoomAsync(Guid id, UpdateRoomRequest request, CancellationToken ct = default);
    Task DeleteRoomAsync(Guid id, CancellationToken ct = default);

    // TimeSlots
    Task<List<TimeSlotDto>> GetAllTimeSlotsAsync(CancellationToken ct = default);
    Task<TimeSlotDto> GetTimeSlotByIdAsync(Guid id, CancellationToken ct = default);
    Task<TimeSlotDto> CreateTimeSlotAsync(CreateTimeSlotRequest request, CancellationToken ct = default);
    Task<TimeSlotDto> UpdateTimeSlotAsync(Guid id, UpdateTimeSlotRequest request, CancellationToken ct = default);
    Task DeleteTimeSlotAsync(Guid id, CancellationToken ct = default);

    // ClassSchedules
    Task<PagedResult<ClassScheduleDto>> GetSchedulesPagedAsync(ClassScheduleQuery query, CancellationToken ct = default);
    Task<ClassScheduleDto> GetScheduleByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClassScheduleDto> CreateScheduleAsync(CreateClassScheduleRequest request, CancellationToken ct = default);
    Task<ClassScheduleDto> UpdateScheduleAsync(Guid id, UpdateClassScheduleRequest request, CancellationToken ct = default);
    Task DeleteScheduleAsync(Guid id, CancellationToken ct = default);

    // ClassSessions
    Task<PagedResult<ClassSessionDto>> GetSessionsPagedAsync(ClassSessionQuery query, CancellationToken ct = default);
    Task<ClassSessionDto> GetSessionByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClassSessionDto> CreateSessionAsync(CreateClassSessionRequest request, CancellationToken ct = default);
    Task<ClassSessionDto> UpdateSessionAsync(Guid id, UpdateClassSessionRequest request, CancellationToken ct = default);
    Task DeleteSessionAsync(Guid id, CancellationToken ct = default);
}
