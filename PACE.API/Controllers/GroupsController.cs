using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public class GroupsController(IGroupService groupService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<GroupDto>>> GetAll([FromQuery] GroupQuery query, CancellationToken ct)
        => Ok(await groupService.GetPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GroupDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await groupService.GetDetailAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
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

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<List<GroupMemberDto>>> GetMembers(Guid id, CancellationToken ct)
        => Ok(await groupService.GetMembersAsync(id, ct));

    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = Roles.Staff)]
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
}
