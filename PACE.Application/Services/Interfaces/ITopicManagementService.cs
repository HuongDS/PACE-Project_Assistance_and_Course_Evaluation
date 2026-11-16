using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface ITopicManagementService
{
    Task<List<CourseTopicDto>> GetCourseTopicsAsync(Guid courseId, CancellationToken ct = default);
    Task<CourseTopicDto> CreateCourseTopicAsync(Guid courseId, CreateCourseTopicRequest request, CancellationToken ct = default);
    Task<CourseTopicDto> UpdateCourseTopicAsync(Guid topicId, UpdateCourseTopicRequest request, CancellationToken ct = default);
    Task DeleteCourseTopicAsync(Guid topicId, CancellationToken ct = default);

    Task<GroupDetailDto> SelectTopicAsync(Guid groupId, SelectTopicRequest request, CancellationToken ct = default);
    Task<GroupDetailDto> ProposeTopicAsync(Guid groupId, ProposeTopicRequest request, CancellationToken ct = default);
}
