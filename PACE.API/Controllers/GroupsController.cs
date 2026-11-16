using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public class GroupsController(
    IGroupService groupService,
    ITopicManagementService topicService,
    IGroupChangeRequestService changeRequestService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<GroupDto>>> GetAll([FromQuery] GroupQuery query, CancellationToken ct)
        => Ok(await groupService.GetPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GroupDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await groupService.GetDetailAsync(id, ct));

    /// <summary>Tạo nhóm mới (Sinh viên tạo nhóm cho lớp đang học hoặc Staff tạo cho lớp).</summary>
    [HttpPost]
    public async Task<ActionResult<GroupDto>> Create([FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        var result = await groupService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GroupDto>> Update(Guid id, [FromBody] UpdateGroupRequest request, CancellationToken ct)
        => Ok(await groupService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await groupService.DeleteAsync(id, ct);
        return NoContent();
    }

    // ==========================================
    // Members & Roles
    // ==========================================

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<List<GroupMemberDto>>> GetMembers(Guid id, CancellationToken ct)
        => Ok(await groupService.GetMembersAsync(id, ct));

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<GroupMemberDto>> AddMember(Guid id, [FromBody] AddGroupMemberRequest request, CancellationToken ct)
    {
        var result = await groupService.AddMemberAsync(id, request, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/members/{studentCode}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<GroupMemberDto>> UpdateMemberRole(Guid id, string studentCode, [FromBody] UpdateGroupMemberRoleRequest request, CancellationToken ct)
        => Ok(await groupService.UpdateMemberRoleAsync(id, studentCode, request, ct));

    [HttpDelete("{id:guid}/members/{studentCode}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> RemoveMember(Guid id, string studentCode, CancellationToken ct)
    {
        await groupService.RemoveMemberAsync(id, studentCode, ct);
        return NoContent();
    }

    /// <summary>Bầu nhóm trưởng hoặc bàn giao quyền nhóm trưởng.</summary>
    [HttpPut("{id:guid}/leader")]
    public async Task<ActionResult<GroupMemberDto>> ElectLeader(Guid id, [FromBody] ElectLeaderRequest request, CancellationToken ct)
        => Ok(await groupService.ElectLeaderAsync(id, request, ct));

    // ==========================================
    // Invitations
    // ==========================================

    /// <summary>Nhóm trưởng gửi lời mời gia nhập nhóm cho sinh viên cùng lớp.</summary>
    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<GroupInvitationDto>> SendInvitation(Guid id, [FromBody] SendInvitationRequest request, CancellationToken ct)
        => Ok(await groupService.SendInvitationAsync(id, request, ct));

    /// <summary>Xem danh sách lời mời đã gửi của nhóm.</summary>
    [HttpGet("{id:guid}/invitations")]
    public async Task<ActionResult<List<GroupInvitationDto>>> GetInvitations(Guid id, CancellationToken ct)
        => Ok(await groupService.GetGroupInvitationsAsync(id, ct));

    // ==========================================
    // Topic Selection & Proposal
    // ==========================================

    /// <summary>Nhóm trưởng chọn đề tài có sẵn từ danh mục môn học.</summary>
    [HttpPost("{id:guid}/topics/select")]
    public async Task<ActionResult<GroupDetailDto>> SelectTopic(Guid id, [FromBody] SelectTopicRequest request, CancellationToken ct)
        => Ok(await topicService.SelectTopicAsync(id, request, ct));

    /// <summary>Nhóm trưởng đề xuất đề tài mới gửi giảng viên phê duyệt.</summary>
    [HttpPost("{id:guid}/topics/propose")]
    public async Task<ActionResult<GroupDetailDto>> ProposeTopic(Guid id, [FromBody] ProposeTopicRequest request, CancellationToken ct)
        => Ok(await topicService.ProposeTopicAsync(id, request, ct));

    // ==========================================
    // Group Change Requests (Sau khi đề tài đã duyệt)
    // ==========================================

    /// <summary>Gửi đơn xin đổi đề tài (chỉ dành cho Nhóm trưởng sau khi đề tài đã duyệt).</summary>
    [HttpPost("{id:guid}/change-requests/topic")]
    public async Task<ActionResult<GroupChangeRequestDto>> RequestTopicChange(Guid id, [FromBody] CreateTopicChangeRequest request, CancellationToken ct)
        => Ok(await changeRequestService.CreateTopicChangeRequestAsync(id, request, ct));

    /// <summary>Gửi đơn xin thay đổi thành viên (Thêm thành viên do Leader gửi; Rút thành viên do Leader hoặc chính SV gửi).</summary>
    [HttpPost("{id:guid}/change-requests/member")]
    public async Task<ActionResult<GroupChangeRequestDto>> RequestMemberChange(Guid id, [FromBody] CreateMemberChangeRequest request, CancellationToken ct)
        => Ok(await changeRequestService.CreateMemberChangeRequestAsync(id, request, ct));

    /// <summary>Gửi đơn xin đổi nhóm trưởng.</summary>
    [HttpPost("{id:guid}/change-requests/leader")]
    public async Task<ActionResult<GroupChangeRequestDto>> RequestLeaderChange(Guid id, [FromBody] CreateLeaderChangeRequest request, CancellationToken ct)
        => Ok(await changeRequestService.CreateLeaderChangeRequestAsync(id, request, ct));

    /// <summary>Xem danh sách các yêu cầu thay đổi của nhóm.</summary>
    [HttpGet("{id:guid}/change-requests")]
    public async Task<ActionResult<List<GroupChangeRequestDto>>> GetChangeRequests(Guid id, CancellationToken ct)
        => Ok(await changeRequestService.GetGroupChangeRequestsAsync(id, ct));

    /// <summary>Hủy đơn yêu cầu thay đổi khi còn ở trạng thái Pending.</summary>
    [HttpDelete("{id:guid}/change-requests/{requestId:guid}")]
    public async Task<IActionResult> CancelChangeRequest(Guid id, Guid requestId, CancellationToken ct)
    {
        await changeRequestService.CancelChangeRequestAsync(id, requestId, ct);
        return NoContent();
    }
}
