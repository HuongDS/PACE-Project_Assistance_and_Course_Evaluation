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

public static class CurrentUserExtensions
{
    /// <summary>Admin và phòng đào tạo quản lý lớp, phân giảng viên và danh sách sinh viên.</summary>
    public static bool ManagesClasses(this ICurrentUserService user) => user.IsAdmin || user.Role == UserRole.TrainingOffice;
}
