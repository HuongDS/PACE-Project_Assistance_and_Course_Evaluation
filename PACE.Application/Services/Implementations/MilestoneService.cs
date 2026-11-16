using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class MilestoneService(
    IUnitOfWork uow,
    ICurrentUserService currentUser) : IMilestoneService
{
    private async Task EnsureStaffAccessAsync(Guid classId, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        throw new ForbiddenException("Chỉ quản trị viên hoặc giảng viên phụ trách lớp mới có quyền thực hiện.");
    }

    public async Task<List<MilestoneDto>> GetByClassIdAsync(Guid classId, CancellationToken ct = default)
    {
        var classExists = await uow.Classes.ClassExistsAsync(classId, ct);
        if (!classExists)
            throw new NotFoundException("Lớp học không tồn tại.");

        var milestones = await uow.Milestones.GetByClassIdAsync(classId, ct);
        return milestones.Select(MapToDto).ToList();
    }

    public async Task<MilestoneDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var milestone = await uow.Milestones.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Mốc tiến độ (Milestone) không tồn tại.");

        return MapToDto(milestone);
    }

    public async Task<MilestoneDto> CreateAsync(Guid classId, CreateMilestoneRequest request, CancellationToken ct = default)
    {
        await EnsureStaffAccessAsync(classId, ct);

        var classExists = await uow.Classes.ClassExistsAsync(classId, ct);
        if (!classExists)
            throw new NotFoundException("Lớp học không tồn tại.");

        if (request.StartDate.HasValue && request.Deadline <= request.StartDate.Value)
            throw new BadRequestException("Hạn chót (Deadline) phải diễn ra sau ngày bắt đầu.");

        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ClassId = classId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Type = request.Type,
            StartDate = request.StartDate,
            Deadline = request.Deadline,
            IsLocked = false,
            OrderIndex = request.OrderIndex,
            CreatedAt = DateTime.UtcNow
        };

        await uow.Milestones.AddAsync(milestone, ct);
        await uow.SaveChangesAsync(ct);

        return MapToDto(milestone);
    }

    public async Task<MilestoneDto> UpdateAsync(Guid id, UpdateMilestoneRequest request, CancellationToken ct = default)
    {
        var milestone = await uow.Milestones.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Mốc tiến độ (Milestone) không tồn tại.");

        await EnsureStaffAccessAsync(milestone.ClassId, ct);

        if (request.StartDate.HasValue && request.Deadline <= request.StartDate.Value)
            throw new BadRequestException("Hạn chót (Deadline) phải diễn ra sau ngày bắt đầu.");

        milestone.Title = request.Title.Trim();
        milestone.Description = request.Description?.Trim();
        milestone.Type = request.Type;
        milestone.StartDate = request.StartDate;
        milestone.Deadline = request.Deadline;
        milestone.IsLocked = request.IsLocked;
        milestone.OrderIndex = request.OrderIndex;
        milestone.UpdatedAt = DateTime.UtcNow;

        uow.Milestones.Update(milestone);
        await uow.SaveChangesAsync(ct);

        return MapToDto(milestone);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var milestone = await uow.Milestones.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Mốc tiến độ (Milestone) không tồn tại.");

        await EnsureStaffAccessAsync(milestone.ClassId, ct);

        uow.Milestones.Remove(milestone);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<MilestoneDto> ToggleLockAsync(Guid id, bool isLocked, CancellationToken ct = default)
    {
        var milestone = await uow.Milestones.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Mốc tiến độ (Milestone) không tồn tại.");

        await EnsureStaffAccessAsync(milestone.ClassId, ct);

        milestone.IsLocked = isLocked;
        milestone.UpdatedAt = DateTime.UtcNow;

        uow.Milestones.Update(milestone);
        await uow.SaveChangesAsync(ct);

        return MapToDto(milestone);
    }

    private static MilestoneDto MapToDto(Milestone m) => new(
        m.Id,
        m.ClassId,
        m.Title,
        m.Description,
        m.Type,
        m.StartDate,
        m.Deadline,
        m.IsLocked,
        m.OrderIndex,
        m.CreatedAt,
        m.UpdatedAt);
}
