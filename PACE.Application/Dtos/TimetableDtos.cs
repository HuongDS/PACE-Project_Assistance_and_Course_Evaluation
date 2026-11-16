using System.ComponentModel.DataAnnotations;
using PACE.Application.Common.Models;

namespace PACE.Application.Dtos;

// Room DTOs
public record RoomDto(
    Guid Id,
    string Code,
    string? Building);

public class CreateRoomRequest
{
    [Required(ErrorMessage = "Mã phòng học không được để trống.")]
    [MaxLength(50, ErrorMessage = "Mã phòng học tối đa 50 ký tự.")]
    public string Code { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Tên tòa nhà tối đa 100 ký tự.")]
    public string? Building { get; set; }
}

public class UpdateRoomRequest
{
    [Required(ErrorMessage = "Mã phòng học không được để trống.")]
    [MaxLength(50, ErrorMessage = "Mã phòng học tối đa 50 ký tự.")]
    public string Code { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Tên tòa nhà tối đa 100 ký tự.")]
    public string? Building { get; set; }
}

// TimeSlot DTOs
public record TimeSlotDto(
    Guid Id,
    int PeriodNumber,
    TimeOnly StartTime,
    TimeOnly EndTime);

public class CreateTimeSlotRequest
{
    [Range(1, 20, ErrorMessage = "Thứ tự tiết học từ 1 đến 20.")]
    public int PeriodNumber { get; set; }

    [Required(ErrorMessage = "Giờ bắt đầu không được để trống.")]
    public TimeOnly StartTime { get; set; }

    [Required(ErrorMessage = "Giờ kết thúc không được để trống.")]
    public TimeOnly EndTime { get; set; }
}

public class UpdateTimeSlotRequest
{
    [Range(1, 20, ErrorMessage = "Thứ tự tiết học từ 1 đến 20.")]
    public int PeriodNumber { get; set; }

    [Required(ErrorMessage = "Giờ bắt đầu không được để trống.")]
    public TimeOnly StartTime { get; set; }

    [Required(ErrorMessage = "Giờ kết thúc không được để trống.")]
    public TimeOnly EndTime { get; set; }
}

// ClassSchedule DTOs
public record ClassScheduleDto(
    Guid Id,
    Guid ClassId,
    string? ClassName,
    string LecturerCode,
    string? LecturerName,
    Guid RoomId,
    string? RoomCode,
    Guid TimeSlotId,
    int PeriodNumber,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int DayOfWeek,
    DateOnly ValidFrom,
    DateOnly ValidTo);

public class ClassScheduleQuery : PageQuery
{
    public Guid? ClassId { get; set; }
    public string? LecturerCode { get; set; }
    public Guid? RoomId { get; set; }
    public int? DayOfWeek { get; set; }
}

public class CreateClassScheduleRequest
{
    [Required(ErrorMessage = "ClassId không được để trống.")]
    public Guid ClassId { get; set; }

    [Required(ErrorMessage = "LecturerCode không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã giảng viên tối đa 20 ký tự.")]
    public string LecturerCode { get; set; } = null!;

    [Required(ErrorMessage = "RoomId không được để trống.")]
    public Guid RoomId { get; set; }

    [Required(ErrorMessage = "TimeSlotId không được để trống.")]
    public Guid TimeSlotId { get; set; }

    [Range(0, 6, ErrorMessage = "Thứ trong tuần từ 0 (Chủ nhật) đến 6 (Thứ bảy).")]
    public int DayOfWeek { get; set; }

    [Required(ErrorMessage = "Ngày áp dụng từ (ValidFrom) không được để trống.")]
    public DateOnly ValidFrom { get; set; }

    [Required(ErrorMessage = "Ngày áp dụng đến (ValidTo) không được để trống.")]
    public DateOnly ValidTo { get; set; }
}

public class UpdateClassScheduleRequest
{
    [Required(ErrorMessage = "LecturerCode không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã giảng viên tối đa 20 ký tự.")]
    public string LecturerCode { get; set; } = null!;

    [Required(ErrorMessage = "RoomId không được để trống.")]
    public Guid RoomId { get; set; }

    [Required(ErrorMessage = "TimeSlotId không được để trống.")]
    public Guid TimeSlotId { get; set; }

    [Range(0, 6, ErrorMessage = "Thứ trong tuần từ 0 (Chủ nhật) đến 6 (Thứ bảy).")]
    public int DayOfWeek { get; set; }

    [Required(ErrorMessage = "Ngày áp dụng từ (ValidFrom) không được để trống.")]
    public DateOnly ValidFrom { get; set; }

    [Required(ErrorMessage = "Ngày áp dụng đến (ValidTo) không được để trống.")]
    public DateOnly ValidTo { get; set; }
}

// ClassSession DTOs
public record ClassSessionDto(
    Guid Id,
    Guid ClassId,
    string? ClassName,
    Guid? ScheduleId,
    DateOnly LearnDate,
    Guid TimeSlotId,
    int PeriodNumber,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid RoomId,
    string? RoomCode,
    string LecturerCode,
    string? LecturerName,
    bool IsCancelled,
    string? Note);

public class ClassSessionQuery : PageQuery
{
    public Guid? ClassId { get; set; }
    public DateOnly? LearnDate { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? LecturerCode { get; set; }
    public Guid? RoomId { get; set; }
    public bool? IsCancelled { get; set; }
}

public class CreateClassSessionRequest
{
    [Required(ErrorMessage = "ClassId không được để trống.")]
    public Guid ClassId { get; set; }

    public Guid? ScheduleId { get; set; }

    [Required(ErrorMessage = "Ngày học không được để trống.")]
    public DateOnly LearnDate { get; set; }

    [Required(ErrorMessage = "TimeSlotId không được để trống.")]
    public Guid TimeSlotId { get; set; }

    [Required(ErrorMessage = "RoomId không được để trống.")]
    public Guid RoomId { get; set; }

    [Required(ErrorMessage = "LecturerCode không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã giảng viên tối đa 20 ký tự.")]
    public string LecturerCode { get; set; } = null!;

    public bool IsCancelled { get; set; } = false;

    [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Note { get; set; }
}

public class UpdateClassSessionRequest
{
    [Required(ErrorMessage = "Ngày học không được để trống.")]
    public DateOnly LearnDate { get; set; }

    [Required(ErrorMessage = "TimeSlotId không được để trống.")]
    public Guid TimeSlotId { get; set; }

    [Required(ErrorMessage = "RoomId không được để trống.")]
    public Guid RoomId { get; set; }

    [Required(ErrorMessage = "LecturerCode không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã giảng viên tối đa 20 ký tự.")]
    public string LecturerCode { get; set; } = null!;

    public bool IsCancelled { get; set; } = false;

    [MaxLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Note { get; set; }
}
