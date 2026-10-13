namespace PACE.Application.Common.Exceptions;

public abstract class AppException(string message, int statusCode, Exception? inner = null) : Exception(message, inner)
{
    public int StatusCode { get; } = statusCode;
}

public class BadRequestException(string message, Exception? inner = null) : AppException(message, 400, inner);

public class UnauthorizedException(string message) : AppException(message, 401);

public class ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.") : AppException(message, 403);

public class NotFoundException : AppException
{
    public NotFoundException(string resource, object key) : base($"Không tìm thấy {resource} với định danh: {key}", 404) { }

    public NotFoundException(string message) : base(message, 404) { }
}

public class ConflictException(string message, Exception? inner = null) : AppException(message, 409, inner);

public class UnprocessableException(string message, Exception? inner = null) : AppException(message, 422, inner);

public class ExternalServiceException(string serviceName, string message, Exception? inner = null)
    : AppException($"[{serviceName}] {message}", 502, inner)
{
    public string ServiceName { get; } = serviceName;
}
