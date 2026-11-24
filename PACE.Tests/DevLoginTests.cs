using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PACE.API.Common;
using PACE.API.Controllers;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Identity;
using Xunit;

namespace PACE.Tests;

public class DevLoginTests
{
    private readonly User lecturer = new() { Id = Guid.NewGuid(), Email = "lecturer@fpt.edu.vn", Role = UserRole.Lecturer };
    private readonly JwtOptions options = new() { Key = "test-signing-key-at-least-32-characters", Issuer = "PACE", Audience = "PACE.Client", ExpiresMinutes = 30 };
    private readonly List<string> lookedUp = [];
    private int tokens;

    private DevLoginService Service(User? found)
    {
        var users = Stub<IUserRepository>.Create((m, a) =>
        {
            Assert.Equal("GetByEmailAsync", m.Name);
            lookedUp.Add((string)a[0]!);
            return Task.FromResult(found);
        });
        var uow = Stub<IUnitOfWork>.Create((m, _) =>
        {
            if (m.Name == "get_Users") return users;
            throw new Exception("Dev login must not write to the database");
        });
        var realJwt = new JwtTokenService(Options.Create(options));
        var jwt = Stub<IJwtTokenService>.Create((_, a) =>
        {
            tokens++;
            return realJwt.CreateToken((User)a[0]!);
        });
        return new DevLoginService(uow, jwt);
    }

    [Fact]
    public async Task ExistingUserGetsRealJwtWithRoleFromDatabase()
    {
        var response = await Service(lecturer).LoginAsync(new DevLoginRequest { Email = lecturer.Email });

        Assert.Equal(1, tokens);
        Assert.Equal(lecturer.Id, response.User.Id);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(response.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = JwtTokenService.SigningKey(options.Key)
        });
        Assert.True(result.IsValid);
        Assert.Equal(lecturer.Id.ToString(), result.Claims[ClaimTypes.NameIdentifier]);
        Assert.Equal(lecturer.Email, result.Claims[ClaimTypes.Email]);
        Assert.Equal("Lecturer", result.Claims[ClaimTypes.Role]);
    }

    [Fact]
    public async Task EmailIsTrimmedAndLowercasedBeforeLookup()
    {
        await Service(lecturer).LoginAsync(new DevLoginRequest { Email = "  Lecturer@FPT.edu.vn " });
        Assert.Equal(new[] { "lecturer@fpt.edu.vn" }, lookedUp);
    }

    [Fact]
    public async Task UnknownEmailIsRejectedWithoutToken()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(null).LoginAsync(new DevLoginRequest { Email = "nobody@fpt.edu.vn" }));
        Assert.Equal(0, tokens);
    }

    [Fact]
    public async Task BlankEmailIsRejectedWithoutLookup()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => Service(lecturer).LoginAsync(new DevLoginRequest { Email = "   " }));
        Assert.Empty(lookedUp);
        Assert.Equal(0, tokens);
    }

    [Fact]
    public void DisabledDevLoginRemovesDevAuthController()
    {
        var feature = new ControllerFeature();
        feature.Controllers.Add(typeof(DevAuthController).GetTypeInfo());
        feature.Controllers.Add(typeof(AuthController).GetTypeInfo());

        new ExcludeDevAuthControllerFeatureProvider().PopulateFeature(Array.Empty<ApplicationPart>(), feature);

        Assert.DoesNotContain(typeof(DevAuthController).GetTypeInfo(), feature.Controllers);
        Assert.Contains(typeof(AuthController).GetTypeInfo(), feature.Controllers);
    }
}
