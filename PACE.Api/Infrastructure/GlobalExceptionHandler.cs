using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PACE.BLL.Common;
using PACE.BLL.Exceptions;

namespace PACE.Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env) : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, message) = Map(exception, httpContext);

        if (statusCode >= 500) logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else logger.LogWarning("Request failed [{Status}]: {Message}", statusCode, message);

        var response = new ErrorResponse { StatusCode = statusCode, Error = message, Timestamp = DateTime.UtcNow };
        if (env.IsDevelopment())
        {
            response.ExceptionType = exception.GetType().Name;
            response.StackTrace = exception.StackTrace;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions), cancellationToken);
        return true;
    }

    private static (int StatusCode, string Message) Map(Exception ex, HttpContext context) => ex switch
    {
        AppException app => (app.StatusCode, app.Message),
        DbUpdateException { InnerException: PostgresException pg } => MapPostgres(pg),
        BadHttpRequestException bad => (bad.StatusCode, bad.Message),
        ArgumentException => (400, ex.Message),
        UnauthorizedAccessException => (403, "Bạn không có quyền thực hiện thao tác này."),
        OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "Yêu cầu bị huỷ bởi client."),
        TimeoutException or OperationCanceledException => (504, "Yêu cầu bị quá thời gian xử lý."),
        _ => (500, "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.")
    };

    private static (int, string) MapPostgres(PostgresException pg) => pg.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => (409, $"Dữ liệu bị trùng ({pg.ConstraintName})."),
        PostgresErrorCodes.ForeignKeyViolation => (409, $"Dữ liệu đang được tham chiếu hoặc tham chiếu không tồn tại ({pg.ConstraintName})."),
        PostgresErrorCodes.CheckViolation => (400, $"Dữ liệu vi phạm ràng buộc {pg.ConstraintName}."),
        PostgresErrorCodes.NotNullViolation => (400, $"Thiếu dữ liệu bắt buộc ({pg.ColumnName})."),
        _ => (500, "Lỗi cơ sở dữ liệu.")
    };
}
