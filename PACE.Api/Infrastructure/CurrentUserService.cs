using System.Security.Claims;
using PACE.BLL.Common;
using PACE.DAL.Models;

namespace PACE.Api.Infrastructure;

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    public const string AnalysisRunClaim = "analysis_run_id";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public UserRole Role => Enum.TryParse<UserRole>(Principal?.FindFirstValue(ClaimTypes.Role), out var role) ? role : UserRole.Student;

    public Guid? AnalysisRunId => Guid.TryParse(Principal?.FindFirstValue(AnalysisRunClaim), out var id) ? id : null;
}
