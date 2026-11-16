using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface IAttendanceRepository : IGenericRepository<Attendance>
{
    Task<PagedResult<AttendanceDto>> SearchAttendanceAsync(AttendanceQuery query, CancellationToken ct = default);
    Task<List<AttendanceDto>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<Attendance?> GetBySessionAndStudentAsync(Guid sessionId, string studentCode, CancellationToken ct = default);
}
