using System.ComponentModel.DataAnnotations;
using PACE.Application.Common.Models;

namespace PACE.Application.Dtos;

public record AttendanceDto(
    Guid Id,
    Guid SessionId,
    string StudentCode,
    Guid? UserId,
    string? FullName,
    string? Email,
    int Status,
    DateTime? RecordedAt,
    string? Comments);

public class AttendanceQuery : PageQuery
{
    public Guid? SessionId { get; set; }
    public string? StudentCode { get; set; }
    public int? Status { get; set; }
}

public class RecordAttendanceItem
{
    [Required(ErrorMessage = "Mã sinh viên không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã sinh viên tối đa 20 ký tự.")]
    public string StudentCode { get; set; } = null!;

    [Range(0, 3, ErrorMessage = "Trạng thái điểm danh: 0 = Vắng, 1 = Có mặt, 2 = Đi muộn, 3 = Có phép.")]
    public int Status { get; set; } = 1;

    [MaxLength(255, ErrorMessage = "Nhận xét tối đa 255 ký tự.")]
    public string? Comments { get; set; }
}

public class BatchRecordAttendanceRequest
{
    [Required(ErrorMessage = "Danh sách điểm danh không được rỗng.")]
    public List<RecordAttendanceItem> Items { get; set; } = [];
}

public class UpdateAttendanceRequest
{
    [Range(0, 3, ErrorMessage = "Trạng thái điểm danh: 0 = Vắng, 1 = Có mặt, 2 = Đi muộn, 3 = Có phép.")]
    public int Status { get; set; }

    [MaxLength(255, ErrorMessage = "Nhận xét tối đa 255 ký tự.")]
    public string? Comments { get; set; }
}
