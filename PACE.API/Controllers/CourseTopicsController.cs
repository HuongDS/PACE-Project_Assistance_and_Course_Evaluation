using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/courses/{courseId:guid}/topics")]
[Authorize]
public class CourseTopicsController(ITopicManagementService topicService) : ControllerBase
{
    /// <summary>Đề tài mẫu của môn học. Truyền classId để lấy danh sách của kỳ lớp đó và số nhóm đã chọn trong lớp;
    /// hoặc semesterId để lọc theo kỳ.</summary>
    [HttpGet]
    public async Task<ActionResult<List<CourseTopicDto>>> GetTopics(Guid courseId, CancellationToken ct, [FromQuery] Guid? semesterId = null, [FromQuery] Guid? classId = null)
        => Ok(await topicService.GetCourseTopicsAsync(courseId, semesterId, classId, ct));

    /// <summary>Thêm đề tài mẫu vào môn học (Giảng viên / Admin).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<CourseTopicDto>> CreateTopic(Guid courseId, [FromBody] CreateCourseTopicRequest request, CancellationToken ct)
    {
        var result = await topicService.CreateCourseTopicAsync(courseId, request, ct);
        return CreatedAtAction(nameof(GetTopics), new { courseId }, result);
    }

    /// <summary>Cập nhật đề tài mẫu của môn học.</summary>
    [HttpPut("{topicId:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<CourseTopicDto>> UpdateTopic(Guid courseId, Guid topicId, [FromBody] UpdateCourseTopicRequest request, CancellationToken ct)
        => Ok(await topicService.UpdateCourseTopicAsync(topicId, request, ct));

    /// <summary>Xóa hoặc ẩn đề tài mẫu.</summary>
    [HttpDelete("{topicId:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> DeleteTopic(Guid courseId, Guid topicId, CancellationToken ct)
    {
        await topicService.DeleteCourseTopicAsync(topicId, ct);
        return NoContent();
    }
}
