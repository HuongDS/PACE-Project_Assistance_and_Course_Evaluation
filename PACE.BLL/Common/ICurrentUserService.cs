using PACE.DAL.Models;

namespace PACE.BLL.Common;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string Email { get; }
    UserRole Role { get; }
    Guid? AnalysisRunId { get; }
    bool IsAdmin => Role == UserRole.Admin;
}
