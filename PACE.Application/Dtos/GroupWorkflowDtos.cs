using System.ComponentModel.DataAnnotations;

namespace PACE.Application.Dtos;

// ==========================================
// 1. Group & Member Workflow DTOs
// ==========================================

public record GroupInvitationDto(
    Guid Id,
    Guid GroupId,
    string GroupName,
    string InvitedStudentCode,
    string? InvitedStudentName,
    string? InvitedStudentEmail,
    Guid InvitedByUserId,
    string? InvitedByName,
    string Status,
    string? Message,
    DateTime CreatedAt,
    DateTime? RespondedAt);

public class SendInvitationRequest
{
    [Required(ErrorMessage = "Mã sinh viên không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã sinh viên tối đa 20 ký tự.")]
    public string StudentCode { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Lời nhắn tối đa 500 ký tự.")]
    public string? Message { get; set; }
}

public class RespondInvitationRequest
{
    public bool Accept { get; set; }
}

public class ElectLeaderRequest
{
    [Required(ErrorMessage = "Mã sinh viên nhóm trưởng mới không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã sinh viên tối đa 20 ký tự.")]
    public string NewLeaderStudentCode { get; set; } = null!;
}

public record AvailableStudentDto(
    string StudentCode,
    Guid? UserId,
    string? FullName,
    string? Email);

public class ClassGroupLockRequest
{
    public bool IsLocked { get; set; }
    public DateTime? RegistrationDeadline { get; set; }
}

public record AutoAssignGroupResultDto(
    int AssignedStudentsCount,
    int CreatedGroupsCount,
    List<string> Details);

// ==========================================
// 2. Course Topic Management DTOs
// ==========================================

public record CourseTopicDto(
    Guid Id,
    Guid CourseId,
    string Title,
    string? Description,
    string? Requirements,
    int MaxGroups,
    int CurrentGroupsCount,
    bool IsActive,
    DateTime CreatedAt);

public class CreateCourseTopicRequest
{
    [Required(ErrorMessage = "Tiêu đề đề tài không được để trống.")]
    [MaxLength(255, ErrorMessage = "Tiêu đề đề tài tối đa 255 ký tự.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }
    public string? Requirements { get; set; }

    [Range(1, 100, ErrorMessage = "Số nhóm tối đa từ 1 đến 100.")]
    public int MaxGroups { get; set; } = 1;
}

public class UpdateCourseTopicRequest
{
    [Required(ErrorMessage = "Tiêu đề đề tài không được để trống.")]
    [MaxLength(255, ErrorMessage = "Tiêu đề đề tài tối đa 255 ký tự.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }
    public string? Requirements { get; set; }

    [Range(1, 100, ErrorMessage = "Số nhóm tối đa từ 1 đến 100.")]
    public int MaxGroups { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}

public class SelectTopicRequest
{
    [Required(ErrorMessage = "Vui lòng chọn đề tài mẫu.")]
    public Guid TopicId { get; set; }
}

public class ProposeTopicRequest
{
    [Required(ErrorMessage = "Tên đề tài đề xuất không được để trống.")]
    [MaxLength(255, ErrorMessage = "Tên đề tài tối đa 255 ký tự.")]
    public string TopicName { get; set; } = null!;

    public string? Description { get; set; }

    [MaxLength(500, ErrorMessage = "Đường dẫn Git repository tối đa 500 ký tự.")]
    public string? RepoUrl { get; set; }
}

// ==========================================
// 3. Group Change Requests DTOs
// ==========================================

public record GroupChangeRequestDto(
    Guid Id,
    Guid GroupId,
    string GroupName,
    string Type,
    string Status,
    string Title,
    string Reason,
    string PayloadJson,
    bool FailStudentIfRemoved,
    Guid CreatedByUserId,
    string? CreatedByName,
    DateTime CreatedAt,
    Guid? ReviewedByUserId,
    string? ReviewedByName,
    string? ReviewNotes,
    DateTime? ReviewedAt);

public class CreateTopicChangeRequest
{
    [Required(ErrorMessage = "Tên đề tài mới không được để trống.")]
    [MaxLength(255, ErrorMessage = "Tên đề tài mới tối đa 255 ký tự.")]
    public string NewTopicName { get; set; } = null!;

    [Required(ErrorMessage = "Lý do xin đổi đề tài không được để trống.")]
    [MaxLength(2000, ErrorMessage = "Lý do tối đa 2000 ký tự.")]
    public string Reason { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Đường dẫn Git repo tối đa 500 ký tự.")]
    public string? NewRepoUrl { get; set; }
}

public class CreateMemberChangeRequest
{
    [Required(ErrorMessage = "Mã sinh viên không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã sinh viên tối đa 20 ký tự.")]
    public string StudentCode { get; set; } = null!;

    /// <summary>"ADD" để thêm thành viên mới, "REMOVE" để rút/loại khỏi nhóm.</summary>
    [Required(ErrorMessage = "Hành động (ADD hoặc REMOVE) không được để trống.")]
    public string Action { get; set; } = "REMOVE";

    [Required(ErrorMessage = "Lý do thay đổi thành viên không được để trống.")]
    [MaxLength(2000, ErrorMessage = "Lý do tối đa 2000 ký tự.")]
    public string Reason { get; set; } = null!;
}

public class CreateLeaderChangeRequest
{
    [Required(ErrorMessage = "Mã sinh viên nhóm trưởng mới không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã sinh viên tối đa 20 ký tự.")]
    public string NewLeaderStudentCode { get; set; } = null!;

    [Required(ErrorMessage = "Lý do đổi nhóm trưởng không được để trống.")]
    [MaxLength(2000, ErrorMessage = "Lý do tối đa 2000 ký tự.")]
    public string Reason { get; set; } = null!;
}

public class ReviewChangeRequest
{
    public bool IsApproved { get; set; }

    [MaxLength(2000, ErrorMessage = "Ghi chú tối đa 2000 ký tự.")]
    public string? Notes { get; set; }

    /// <summary>Áp dụng khi chấp thuận đơn rút thành viên: Giảng viên có quyền đánh dấu sinh viên rớt môn.</summary>
    public bool FailStudentIfRemoved { get; set; } = false;
}
