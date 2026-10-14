using System.Security.Claims;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.API.Common;

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public UserRole Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : UserRole.Student;

    public Guid? AnalysisRunId => Guid.TryParse(Principal?.FindFirstValue(CustomClaimTypes.AnalysisRunId), out var id) ? id : null;
}
