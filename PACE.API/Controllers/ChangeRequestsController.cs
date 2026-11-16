using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.Staff)]
public class ChangeRequestsController(
    IGroupChangeRequestService changeRequestService,
    IGroupService groupService) : ControllerBase
{
    /// <summary>Lấy danh sách các yêu cầu thay đổi (đổi đề tài, thành viên) của một lớp học.</summary>
    [HttpGet("api/classes/{classId:guid}/change-requests")]
    public async Task<ActionResult<List<GroupChangeRequestDto>>> GetClassChangeRequests(Guid classId, CancellationToken ct)
        => Ok(await changeRequestService.GetClassChangeRequestsAsync(classId, ct));

    /// <summary>Chi tiết một đơn yêu cầu thay đổi.</summary>
    [HttpGet("api/change-requests/{id:guid}")]
    public async Task<ActionResult<GroupChangeRequestDto>> GetChangeRequestById(Guid id, CancellationToken ct)
        => Ok(await changeRequestService.GetChangeRequestByIdAsync(id, ct));

    /// <summary>Giảng viên phê duyệt hoặc từ chối yêu cầu thay đổi (hệ thống tự động thực thi thay đổi nếu chấp thuận).</summary>
    [HttpPost("api/groups/{groupId:guid}/change-requests/{requestId:guid}/review")]
    public async Task<ActionResult<GroupChangeRequestDto>> ReviewChangeRequest(
        Guid groupId, Guid requestId, [FromBody] ReviewChangeRequest request, CancellationToken ct)
        => Ok(await changeRequestService.ReviewChangeRequestAsync(groupId, requestId, request, ct));

    /// <summary>Khóa danh sách nhóm hoặc cập nhật hạn chót tạo nhóm cho lớp học.</summary>
    [HttpPost("api/classes/{classId:guid}/group-lock")]
    public async Task<IActionResult> LockGroupRegistration(Guid classId, [FromBody] ClassGroupLockRequest request, CancellationToken ct)
    {
        await groupService.LockGroupRegistrationAsync(classId, request, ct);
        return NoContent();
    }

    /// <summary>Tự động phân bổ sinh viên chưa có nhóm sau hạn chót (vào nhóm thiếu người hoặc tạo nhóm ghép mới).</summary>
    [HttpPost("api/classes/{classId:guid}/auto-assign-groups")]
    public async Task<ActionResult<AutoAssignGroupResultDto>> AutoAssignUnassignedStudents(Guid classId, CancellationToken ct)
        => Ok(await groupService.AutoAssignUnassignedStudentsAsync(classId, ct));
}
