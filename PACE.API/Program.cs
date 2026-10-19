using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text.Json.Serialization;
using PACE.API.Common;
using PACE.Application.Common.Interfaces;
using PACE.Application;
using PACE.Infrastructure;
using PACE.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<PACE.API.Controllers.TeachingAccess>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var authEnabled = builder.Configuration.GetValue("Auth:Enabled", true);
if (!authEnabled && !builder.Environment.IsDevelopment()) throw new InvalidOperationException("Authentication cannot be disabled outside Development.");
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (authEnabled)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = JwtTokenService.SigningKey(jwt.Key),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                RoleClaimType = ClaimTypes.Role,
                NameClaimType = ClaimTypes.Email
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal!;
                    if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) { context.Fail("Invalid identity."); return; }
                    var db = context.HttpContext.RequestServices.GetRequiredService<PACE.Infrastructure.Persistence.AppDbContext>();
                    var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(db.Users, u => u.Id == userId, context.HttpContext.RequestAborted);
                    var run = principal.FindFirstValue(PACE.Application.Common.CustomClaimTypes.AnalysisRunId);
                    if (user is null || user.Role.ToString() != principal.FindFirstValue(ClaimTypes.Role) || user.AnalysisRunId?.ToString() != run)
                        context.Fail("Account permissions changed; sign in again.");
                }
            };
        });
}
else
{
    builder.Services.AddAuthentication();
}
builder.Services.AddAuthorization();

const string CorsPolicy = "FrontendPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());
            return new BadRequestObjectResult(new ErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Error = "Dữ liệu gửi lên không hợp lệ.",
                ValidationErrors = errors
            });
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PACE API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Gọi POST /api/auth/login, dán accessToken vào đây.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    if (authEnabled)
    {
        c.AddSecurityDefinition("Bearer", scheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
    }
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
});

var app = builder.Build();

if (authEnabled) _ = JwtTokenService.SigningKey(jwt.Key);
else app.Logger.LogWarning("Auth:Enabled = false: mọi endpoint đang mở, không cần đăng nhập (chỉ dùng khi test).");

app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var revision = app.Configuration["Deployment:Revision"];
        if (!string.IsNullOrWhiteSpace(revision)) context.Response.Headers["X-PACE-Revision"] = revision;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(report.Status.ToString());
    }
}).AllowAnonymous();
var controllers = app.MapControllers();
if (!authEnabled) controllers.AllowAnonymous();

await app.SeedAdminsAsync();

app.Run();
