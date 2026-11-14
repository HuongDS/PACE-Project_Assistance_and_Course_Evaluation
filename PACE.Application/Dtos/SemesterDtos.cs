using System.ComponentModel.DataAnnotations;

namespace PACE.Application.Dtos;

public record SemesterDto(
    Guid Id,
    string Name,
    string AcademicYear,
    DateOnly StartDate,
    DateOnly ActualStartDate,
    DateOnly EndDate,
    bool IsCurrent,
    int ClassCount);

public class CreateSemesterRequest
{
    [Required(ErrorMessage = "Tên học kỳ không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên học kỳ tối đa 100 ký tự.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Năm học không được để trống.")]
    [MaxLength(20, ErrorMessage = "Năm học tối đa 20 ký tự.")]
    public string AcademicYear { get; set; } = null!;

    [Required(ErrorMessage = "Ngày bắt đầu học kỳ không được để trống.")]
    public DateOnly StartDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    [Required(ErrorMessage = "Ngày kết thúc học kỳ không được để trống.")]
    public DateOnly EndDate { get; set; }
}

public class UpdateSemesterRequest
{
    [Required(ErrorMessage = "Tên học kỳ không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên học kỳ tối đa 100 ký tự.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "Năm học không được để trống.")]
    [MaxLength(20, ErrorMessage = "Năm học tối đa 20 ký tự.")]
    public string AcademicYear { get; set; } = null!;

    [Required(ErrorMessage = "Ngày bắt đầu học kỳ không được để trống.")]
    public DateOnly StartDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    [Required(ErrorMessage = "Ngày kết thúc học kỳ không được để trống.")]
    public DateOnly EndDate { get; set; }
}

