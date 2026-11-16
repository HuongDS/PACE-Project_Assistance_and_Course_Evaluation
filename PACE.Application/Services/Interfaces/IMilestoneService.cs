using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IMilestoneService
{
    Task<List<MilestoneDto>> GetByClassIdAsync(Guid classId, CancellationToken ct = default);
    Task<MilestoneDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MilestoneDto> CreateAsync(Guid classId, CreateMilestoneRequest request, CancellationToken ct = default);
    Task<MilestoneDto> UpdateAsync(Guid id, UpdateMilestoneRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<MilestoneDto> ToggleLockAsync(Guid id, bool isLocked, CancellationToken ct = default);
}
