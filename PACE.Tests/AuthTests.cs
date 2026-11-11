using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PACE.API.Common;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Identity;
using Xunit;

namespace PACE.Tests;

public class AuthTests
{
    private readonly User student = new() { Id = Guid.NewGuid(), StudentCode = "SE123456", Email = "student@gmail.com", Role = UserRole.Student };
    private readonly JwtOptions options = new() { Key = "test-signing-key-at-least-32-characters", Issuer = "PACE", Audience = "PACE.Client", ExpiresMinutes = 30 };
    private int lookups;
    private int tokens;
    private int saves;

    private AuthService Service(User? found, bool invalid = false, bool emailTaken = false, bool saveFails = false)
    {
        var users = Stub<IUserRepository>.Create((m, a) =>
        {
            if (m.Name == "EmailExistsAsync")
            {
                Assert.Equal(student.Id, a[1]);
                return Task.FromResult(emailTaken);
            }
            if (m.Name == "GetWithRepoAsync")
            {
                Assert.Equal(student.Id, a[0]);
                Assert.Equal(false, a[1]);
                return Task.FromResult(found);
            }
            if (m.Name == "GetByStudentCodeAsync")
            {
                var code = (string)a[0]!;
                Assert.Equal(code.Trim().ToUpperInvariant(), code);
                lookups++;
                return Task.FromResult(string.Equals(student.StudentCode?.Trim(), code, StringComparison.OrdinalIgnoreCase) ? found : null);
            }
            Assert.Equal("GetByEmailAsync", m.Name);
            Assert.Equal((found ?? student).Email, a[0]);
            lookups++;
            return Task.FromResult(found);
        });
        var uow = Stub<IUnitOfWork>.Create((m, _) =>
        {
            if (m.Name == "get_Users") return users;
            if (m.Name == "SaveChangesAsync")
            {
                saves++;
                return saveFails ? Task.FromException<int>(new InvalidOperationException("Database unavailable")) : Task.FromResult(1);
            }
            throw new Exception("Unexpected database operation");
        });
        var google = Stub<IGoogleIdTokenValidator>.Create((_, a) =>
        {
            Assert.Equal("google-id-token", a[0]);
            return invalid ? Task.FromException<string>(new UnauthorizedException("Invalid token")) : Task.FromResult(student.Email);
        });
        var jwt = Stub<IJwtTokenService>.Create((_, a) =>
        {
            tokens++;
            return new JwtTokenService(Options.Create(options)).CreateToken((User)a[0]!);
        });
        var hasher = Stub<IPasswordHasher>.Create((m, a) =>
        {
            if (m.Name == "HashPassword") return $"hashed_{(string)a[0]!}";
            if (m.Name == "VerifyPassword") return (string)a[0]! == "correct-password";
            return false;
        });
        var otpCache = Stub<IOtpCacheService>.Create((_, _) => Task.CompletedTask);
        var emailService = Stub<IEmailService>.Create((_, _) => Task.CompletedTask);

        return new AuthService(uow, jwt, Stub<ICurrentUserService>.Create((_, _) => throw new NotSupportedException()), google, hasher, otpCache, emailService, Stub<IAdminCredentialValidator>.Create((_, a) => (string)a[0]! == "correct-test-password"));
    }

    [Fact]
    public async Task StudentLoginIssuesJwtThatResolvesUuidAndRole()
    {
        var result = await Service(student).GoogleStudentLoginAsync(new() { IdToken = "google-id-token" });
        Assert.Equal(student.Id, result.Student.UserId);
        Assert.Equal(student.StudentCode, result.Student.StudentId);
        Assert.Equal(student.Email, result.Student.Email);
        Assert.InRange(result.ExpiresAt, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(result.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = options.Issuer, ValidAudience = options.Audience,
            IssuerSigningKey = JwtTokenService.SigningKey(options.Key),
            ValidateIssuerSigningKey = true, ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role, NameClaimType = ClaimTypes.Email
        });
        Assert.True(validation.IsValid, validation.Exception?.Message);
        var current = new CurrentUserService(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(validation.ClaimsIdentity) }
        });
        Assert.True(current.IsAuthenticated);
        Assert.Equal(student.Id, current.UserId);
        Assert.Equal(UserRole.Student, current.Role);
        Assert.Equal(student.Email, current.Email);
    }

    [Fact]
    public async Task UnknownEmailRejectedWithoutIssuingToken()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(null).GoogleStudentLoginAsync(new() { IdToken = "google-id-token" }));
        Assert.Equal(1, lookups);
        Assert.Equal(0, tokens);
    }

    [Fact]
    public async Task GoogleLoginRejectsAdmin()
    {
        student.Role = UserRole.Admin;
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(student).GoogleStudentLoginAsync(new() { IdToken = "google-id-token" }));
        Assert.Equal(0, tokens);
    }

    [Fact]
    public async Task InvalidGoogleTokenNeverQueriesDatabase()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(student, invalid: true).GoogleStudentLoginAsync(new() { IdToken = "google-id-token" }));
        Assert.Equal(0, lookups);
        Assert.Equal(0, tokens);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task BlankTokenRejected(string token)
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(student).GoogleStudentLoginAsync(new() { IdToken = token }));
        Assert.Equal(0, lookups);
    }

    [Fact]
    public async Task LegacyLoginStillAllowsAdmin()
    {
        student.Role = UserRole.Admin;
        var response = await Service(student).LoginAsync(new() { Email = student.Email, Password = "correct-test-password" });
        Assert.Equal(UserRole.Admin, response.User.Role);
        Assert.Equal(1, tokens);
    }

    [Fact]
    public async Task LegacyLoginCannotBypassGoogleForStudents()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(student).LoginAsync(new() { Email = student.Email }));
        Assert.Equal(0, tokens);
    }

    [Fact]
    public async Task MissingGoogleClientIdFailsClosed()
    {
        var validator = new GoogleIdTokenValidator(Options.Create(new GoogleAuthOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateEmailAsync("any-token"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    public async Task GoogleValidatorRejectsMalformedTokens(string token)
    {
        var validator = new GoogleIdTokenValidator(Options.Create(new GoogleAuthOptions { ClientId = "pace.apps.googleusercontent.com" }));
        await Assert.ThrowsAsync<UnauthorizedException>(() => validator.ValidateEmailAsync(token));
    }
    [Fact]
    public async Task AdminWrongPasswordNeverIssuesJwt()
    {
        student.Role = UserRole.Admin;
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(student).LoginAsync(new() { Email = student.Email, Password = "incorrect" }));
        Assert.Equal(0, tokens);
    }
    [Fact]
    public async Task RegisteredLecturerCanUseGoogleStaffLogin()
    {
        student.Role = UserRole.Lecturer;
        var response = await Service(student).GoogleStaffLoginAsync(new() { IdToken = "google-id-token" });
        Assert.Equal(UserRole.Lecturer, response.User.Role);
        Assert.Equal(1, tokens);
    }
    [Fact]
    public async Task StudentCannotUseGoogleStaffLogin()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(student).GoogleStaffLoginAsync(new() { IdToken = "google-id-token" }));
        Assert.Equal(0, tokens);
    }

    [Fact]
    public async Task ManagerLoginSucceedsWithManagerAt123()
    {
        var manager = new User { Id = Guid.NewGuid(), Email = "manager", Role = UserRole.Admin };
        var response = await Service(manager).LoginAsync(new() { Email = "manager", Password = "manager@123" });
        Assert.Equal(UserRole.Admin, response.User.Role);
        Assert.Equal("manager", response.User.Email);
        Assert.Equal(1, tokens);
    }

    [Fact]
    public async Task ManagerLoginRejectsIncorrectPassword()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => Service(null).LoginAsync(new() { Email = "manager", Password = "wrong-password" }));
        Assert.Equal(0, tokens);
    }
}
