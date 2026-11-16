using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IAttendanceService
{
    Task<PagedResult<AttendanceDto>> GetPagedAsync(AttendanceQuery query, CancellationToken ct = default);
    Task<List<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<AttendanceDto>> BatchRecordAsync(Guid sessionId, BatchRecordAttendanceRequest request, CancellationToken ct = default);
    Task<AttendanceDto> UpdateAsync(Guid id, UpdateAttendanceRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
