using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using PACE.API.Controllers;

namespace PACE.API.Common;

/// <summary>
/// Gỡ DevAuthController khỏi danh sách controller khi Dev Login tắt, để endpoint không được đăng ký
/// (không có route, không hiện trong Swagger).
/// </summary>
public sealed class ExcludeDevAuthControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        => feature.Controllers.Remove(typeof(DevAuthController).GetTypeInfo());
}
