using System.ComponentModel.DataAnnotations;
using PACE.Application.Common.Models;

namespace PACE.Application.Dtos;

public record GroupDto(
    Guid Id,
    Guid ClassId,
    string Name,
    string? TopicName,
    DateTime CreatedAt,
    int MemberCount);

public record GroupMemberDto(
    Guid GroupId,
    string StudentCode,
    Guid? UserId,
    string? FullName,
    string? Email,
    string Role,
    DateTime JoinedAt);

public record GroupDetailDto(
    Guid Id,
    Guid ClassId,
    string ClassName,
    string Name,
    string? TopicName,
    DateTime CreatedAt,
    IReadOnlyList<GroupMemberDto> Members);

public class GroupQuery : PageQuery
{
    public Guid? ClassId { get; set; }
    public string? SearchTerm { get; set; }
}

public class CreateGroupRequest
{
    [Required(ErrorMessage = "ClassId không được để trống.")]
    public Guid ClassId { get; set; }

    [Required(ErrorMessage = "Tên nhóm không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên nhóm tối đa 100 ký tự.")]
    public string Name { get; set; } = null!;

    [MaxLength(255, ErrorMessage = "Tên đề tài tối đa 255 ký tự.")]
    public string? TopicName { get; set; }
}

public class UpdateGroupRequest
{
    [Required(ErrorMessage = "Tên nhóm không được để trống.")]
    [MaxLength(100, ErrorMessage = "Tên nhóm tối đa 100 ký tự.")]
    public string Name { get; set; } = null!;

    [MaxLength(255, ErrorMessage = "Tên đề tài tối đa 255 ký tự.")]
    public string? TopicName { get; set; }
}

public class AddGroupMemberRequest
{
    [Required(ErrorMessage = "Mã sinh viên không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã sinh viên tối đa 20 ký tự.")]
    public string StudentCode { get; set; } = null!;

    [MaxLength(50, ErrorMessage = "Vai trò tối đa 50 ký tự.")]
    public string Role { get; set; } = "Member";
}

public class UpdateGroupMemberRoleRequest
{
    [Required(ErrorMessage = "Vai trò không được để trống.")]
    [MaxLength(50, ErrorMessage = "Vai trò tối đa 50 ký tự.")]
    public string Role { get; set; } = "Member";
}
