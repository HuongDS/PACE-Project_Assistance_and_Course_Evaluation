namespace PACE.Domain.Entities;

/// <summary>Tài khoản git sinh viên khai để hệ thống nhận ra ai viết dòng nào trong repo của nhóm.</summary>
public class StudentGitAccount
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>Tên đăng nhập GitHub, lưu chữ thường.</summary>
    public string? GitHubUsername { get; set; }
    /// <summary>Email dùng khi commit, cách nhau bằng dấu phẩy.</summary>
    public string? Emails { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid UpdatedBy { get; set; }
}
