using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/student")]
[Authorize]
public class StudentGroupsController(IGroupService groupService) : ControllerBase
{
    /// <summary>Lấy danh sách các nhóm sinh viên hiện đang tham gia.</summary>
    [HttpGet("my-groups")]
    [HttpGet("groups")]
    public async Task<ActionResult<List<GroupDto>>> GetMyGroups(CancellationToken ct)
        => Ok(await groupService.GetMyGroupsAsync(ct));

    /// <summary>Lấy danh sách các lời mời gia nhập nhóm gửi tới sinh viên hiện tại.</summary>
    [HttpGet("invitations")]
    public async Task<ActionResult<List<GroupInvitationDto>>> GetInvitations(CancellationToken ct)
        => Ok(await groupService.GetStudentInvitationsAsync(ct));

    /// <summary>Sinh viên chấp thuận hoặc từ chối lời mời gia nhập nhóm.</summary>
    [HttpPost("invitations/{id:guid}/respond")]
    public async Task<ActionResult<GroupMemberDto>> RespondInvitation(Guid id, [FromBody] RespondInvitationRequest request, CancellationToken ct)
        => Ok(await groupService.RespondInvitationAsync(id, request, ct));

    /// <summary>Lấy danh sách sinh viên cùng lớp chưa có nhóm để mời vào nhóm.</summary>
    [HttpGet("classes/{classId:guid}/available-students")]
    [HttpGet("classes/{classId:guid}/available-peers")]
    public async Task<ActionResult<List<AvailableStudentDto>>> GetAvailableStudents(Guid classId, CancellationToken ct)
        => Ok(await groupService.GetAvailableStudentsInClassAsync(classId, ct));
}
