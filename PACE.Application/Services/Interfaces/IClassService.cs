using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IClassService
{
    Task<PagedResult<ClassDto>> GetPagedAsync(ClassQuery query, CancellationToken ct = default);

    Task<ClassDetailDto> GetDetailAsync(Guid classId, CancellationToken ct = default);

    Task<PagedResult<ClassStudentDto>> GetStudentsAsync(Guid classId, ClassStudentQuery query, CancellationToken ct = default);

    Task<int> AddStudentsAsync(Guid classId, AddStudentsToClassRequest request, CancellationToken ct = default);

    Task RemoveStudentAsync(Guid classId, string studentCode, CancellationToken ct = default);

    Task<ImportClassStudentsResult> ImportStudentsAsync(Guid classId, Stream excelStream, CancellationToken ct = default);

    Task<(byte[] FileBytes, string FileName)> ExportStudentsAsync(Guid classId, CancellationToken ct = default);

    Task<ClassDto> CreateClassAsync(CreateClassRequest request, CancellationToken ct = default);

    Task<ImportClassesResult> ImportClassesAsync(Stream excelStream, CancellationToken ct = default);

    Task<(byte[] FileBytes, string FileName)> ExportClassTemplateAsync(CancellationToken ct = default);
}

