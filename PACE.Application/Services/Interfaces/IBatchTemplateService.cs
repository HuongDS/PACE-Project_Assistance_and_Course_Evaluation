using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IBatchTemplateService
{
    /// <summary>Đọc link repo từ file Excel và tạo bản nháp template cho từng repo; lỗi ở một repo không làm hỏng các repo khác.</summary>
    Task<BatchTemplateResultDto> GenerateAsync(Stream excel, bool useAi, bool regenerate, CancellationToken ct = default);
}
