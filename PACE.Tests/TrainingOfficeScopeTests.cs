using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using PACE.API.Common;
using PACE.API.Controllers;
using Xunit;

namespace PACE.Tests;

public class TrainingOfficeScopeTests
{
    private static IActionResult? Run(string role, params object[] metadata)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"))
        };
        var action = new ActionContext(http, new RouteData(), new ActionDescriptor { EndpointMetadata = [.. metadata] });
        var context = new AuthorizationFilterContext(action, []);
        new TrainingOfficeScopeFilter().OnAuthorization(context);
        return context.Result;
    }

    [Fact]
    public void TrainingOffice_IsDeniedUnmarkedEndpoints() => Assert.IsType<ForbidResult>(Run("TrainingOffice"));

    [Fact]
    public void TrainingOffice_ReachesMarkedAndAnonymousEndpoints()
    {
        Assert.Null(Run("TrainingOffice", new AllowTrainingOfficeAttribute()));
        Assert.Null(Run("TrainingOffice", new AllowAnonymousAttribute()));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Lecturer")]
    [InlineData("Student")]
    public void OtherRoles_AreNotAffected(string role) => Assert.Null(Run(role));

    [Theory]
    [InlineData(typeof(ClassController), true)]
    [InlineData(typeof(SemestersController), true)]
    [InlineData(typeof(LecturersController), true)]
    [InlineData(typeof(AuthController), true)]
    [InlineData(typeof(GroupsController), false)]
    [InlineData(typeof(ReportsController), false)]
    [InlineData(typeof(UsersController), false)]
    public void OnlyRegistryControllers_AreOpenToTrainingOffice(Type controller, bool open) =>
        Assert.Equal(open, controller.IsDefined(typeof(AllowTrainingOfficeAttribute), inherit: true));
}
