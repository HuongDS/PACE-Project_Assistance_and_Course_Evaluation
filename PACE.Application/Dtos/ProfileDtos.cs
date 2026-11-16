using System.ComponentModel.DataAnnotations;
using PACE.Application.Common.Models;

namespace PACE.Application.Dtos;

public record StudentProfileDto(
    string StudentCode,
    Guid UserId,
    string? FullName,
    string? Email,
    int EnrollmentYear,
    DateTime CreatedAt);

public class StudentProfileQuery : PageQuery
{
    public string? SearchTerm { get; set; }
    public int? EnrollmentYear { get; set; }
}

public class CreateStudentProfileRequest
{
    [Required(ErrorMessage = "Mã sinh viên không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã sinh viên tối đa 20 ký tự.")]
    public string StudentCode { get; set; } = null!;

    [Required(ErrorMessage = "UserId không được để trống.")]
    public Guid UserId { get; set; }

    [Range(2000, 2100, ErrorMessage = "Năm nhập học từ 2000 đến 2100.")]
    public int EnrollmentYear { get; set; }
}

public class UpdateStudentProfileRequest
{
    [Range(2000, 2100, ErrorMessage = "Năm nhập học từ 2000 đến 2100.")]
    public int EnrollmentYear { get; set; }
}

public record LecturerProfileDto(
    string LecturerCode,
    Guid UserId,
    string? FullName,
    string? Email,
    string AcademicTitle,
    DateTime CreatedAt);

public class LecturerProfileQuery : PageQuery
{
    public string? SearchTerm { get; set; }
    public string? AcademicTitle { get; set; }
}

public class CreateLecturerProfileRequest
{
    [Required(ErrorMessage = "Mã giảng viên không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã giảng viên tối đa 20 ký tự.")]
    public string LecturerCode { get; set; } = null!;

    [Required(ErrorMessage = "UserId không được để trống.")]
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "Học hàm / học vị không được để trống.")]
    [MaxLength(100, ErrorMessage = "Học hàm / học vị tối đa 100 ký tự.")]
    public string AcademicTitle { get; set; } = "Lecturer";
}

public class UpdateLecturerProfileRequest
{
    [Required(ErrorMessage = "Học hàm / học vị không được để trống.")]
    [MaxLength(100, ErrorMessage = "Học hàm / học vị tối đa 100 ký tự.")]
    public string AcademicTitle { get; set; } = null!;
}
