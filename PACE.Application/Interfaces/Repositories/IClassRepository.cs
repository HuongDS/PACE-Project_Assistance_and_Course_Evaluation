using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface IClassRepository : IGenericRepository<Class>
{
    Task<PagedResult<ClassDto>> SearchClassesAsync(
        ClassQuery query,
        Guid? lecturerUserId = null,
        string? studentCode = null,
        CancellationToken ct = default);

    Task<ClassDetailDto?> GetClassDetailAsync(Guid classId, CancellationToken ct = default);

    Task<PagedResult<ClassStudentDto>> GetClassStudentsAsync(
        Guid classId,
        ClassStudentQuery query,
        CancellationToken ct = default);

    Task<List<ClassStudentDto>> GetAllStudentsInClassAsync(Guid classId, CancellationToken ct = default);

    Task<bool> IsLecturerOfClassAsync(Guid classId, Guid lecturerUserId, CancellationToken ct = default);

    Task<bool> IsStudentOfClassAsync(Guid classId, Guid studentUserId, string? studentCode = null, CancellationToken ct = default);

    Task<string?> GetStudentCodeByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<int> AddStudentsToClassAsync(Guid classId, IEnumerable<string> studentCodes, CancellationToken ct = default);

    Task<bool> RemoveStudentFromClassAsync(Guid classId, string studentCode, CancellationToken ct = default);

    Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct = default);
}

