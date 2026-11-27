namespace PACE.API.Controllers;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Student = "Student";
    public const string Lecturer = "Lecturer";
    public const string Staff = "Admin,Lecturer";
    public const string TrainingOffice = "TrainingOffice";
    /// <summary>Quản lý học kỳ, môn học, lớp và danh sách sinh viên.</summary>
    public const string Registrar = "Admin,TrainingOffice";
    /// <summary>Đọc dữ liệu lớp phục vụ giảng dạy hoặc học vụ.</summary>
    public const string ClassStaff = "Admin,Lecturer,TrainingOffice";
}
