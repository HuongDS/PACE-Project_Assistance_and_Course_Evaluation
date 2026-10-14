using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Common.Interfaces;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string Email { get; }
    UserRole Role { get; }
    Guid? AnalysisRunId { get; }
    bool IsAdmin => Role == UserRole.Admin;
}
