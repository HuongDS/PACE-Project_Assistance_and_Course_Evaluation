using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PACE.Domain.Enums;

namespace PACE.API.Common;

/// <summary>Đánh dấu controller hoặc action mà phòng đào tạo được gọi.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowTrainingOfficeAttribute : Attribute;

/// <summary>
/// Phòng đào tạo bị từ chối ở mọi nơi không đánh dấu <see cref="AllowTrainingOfficeAttribute"/>.
/// Nhiều xử lý chỉ chặn sinh viên và giảng viên, nên vai trò mới không được tự lọt qua những chỗ đó.
/// </summary>
public sealed class TrainingOfficeScopeFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.User.IsInRole(nameof(UserRole.TrainingOffice))) return;

        var metadata = context.ActionDescriptor.EndpointMetadata;
        if (metadata.Any(x => x is AllowTrainingOfficeAttribute or IAllowAnonymous)) return;

        context.Result = new ForbidResult();
    }
}
