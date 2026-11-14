using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface ISemesterService
{
    Task<List<SemesterDto>> GetAllAsync(CancellationToken ct = default);

    Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct = default);

    Task<SemesterDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<SemesterDto> CreateAsync(CreateSemesterRequest request, CancellationToken ct = default);

    Task<SemesterDto> UpdateAsync(Guid id, UpdateSemesterRequest request, CancellationToken ct = default);
}

