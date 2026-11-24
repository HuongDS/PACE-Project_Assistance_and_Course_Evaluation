using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

/// <summary>
/// Đăng nhập không qua Google, CHỈ dùng khi dev. Chỉ được đăng ký DI khi Auth:DevLoginEnabled = true
/// trong môi trường Development (xem Program.cs).
/// </summary>
public interface IDevLoginService
{
    Task<LoginResponse> LoginAsync(DevLoginRequest request, CancellationToken ct = default);
}
