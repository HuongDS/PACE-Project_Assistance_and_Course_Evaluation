namespace PACE.Domain.Enums;

public enum UserRole
{
    Student,

    Admin,

    Lecturer,

    /// <summary>Phòng đào tạo: quản lý học kỳ, môn học, lớp và danh sách sinh viên; không giảng dạy hay chấm điểm.</summary>
    TrainingOffice
}
