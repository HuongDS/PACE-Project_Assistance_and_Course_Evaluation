using System.ComponentModel.DataAnnotations;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos;

public record MilestoneDto(
    Guid Id,
    Guid ClassId,
    string Title,
    string? Description,
    MilestoneType Type,
    DateTime? StartDate,
    DateTime Deadline,
    bool IsLocked,
    int OrderIndex,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateMilestoneRequest(
    [Required, MaxLength(255)] string Title,
    [MaxLength(2000)] string? Description,
    [Required] MilestoneType Type,
    DateTime? StartDate,
    [Required] DateTime Deadline,
    int OrderIndex = 1);

public record UpdateMilestoneRequest(
    [Required, MaxLength(255)] string Title,
    [MaxLength(2000)] string? Description,
    [Required] MilestoneType Type,
    DateTime? StartDate,
    [Required] DateTime Deadline,
    bool IsLocked,
    int OrderIndex = 1);

public record ToggleMilestoneLockRequest(
    bool IsLocked);
