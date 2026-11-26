using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class TopicManagementService(
    IUnitOfWork uow,
    ICurrentUserService currentUser) : ITopicManagementService
{
    private async Task EnsureStaffAccessAsync(Guid classId, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(classId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        throw new ForbiddenException("Chỉ quản trị viên hoặc giảng viên phụ trách mới có quyền thực hiện.");
    }

    private async Task EnsureLeaderOrStaffAccessAsync(Group group, CancellationToken ct)
    {
        if (currentUser.IsAdmin) return;

        if (currentUser.Role == UserRole.Lecturer)
        {
            var isLecturer = await uow.Classes.IsLecturerOfClassAsync(group.ClassId, currentUser.UserId, ct);
            if (isLecturer) return;
        }

        if (currentUser.Role == UserRole.Student)
        {
            var studentCode = await uow.Classes.GetStudentCodeByUserIdAsync(currentUser.UserId, ct);
            if (!string.IsNullOrEmpty(studentCode))
            {
                var member = await uow.Groups.GetMemberAsync(group.Id, studentCode, ct);
                if (member != null && member.Role.Equals("Leader", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        throw new ForbiddenException("Chỉ nhóm trưởng hoặc giảng viên mới có quyền chọn hoặc đề xuất đề tài cho nhóm.");
    }

    private async Task<Guid> RequireSemesterAsync(Guid semesterId, CancellationToken ct)
    {
        if (!await uow.Semesters.AnyAsync(s => s.Id == semesterId, ct))
            throw new NotFoundException("Kỳ học không tồn tại.");
        return semesterId;
    }

    public async Task<List<CourseTopicDto>> GetCourseTopicsAsync(Guid courseId, Guid? semesterId = null, Guid? classId = null, CancellationToken ct = default)
    {
        if (classId is Guid id)
        {
            var targetClass = await uow.Classes.GetByIdAsync(id, ct)
                ?? throw new NotFoundException("Lớp học không tồn tại.");
            if (targetClass.CourseId != courseId)
                throw new BadRequestException("Lớp này không thuộc môn đang xem.");
            semesterId = targetClass.SemesterId;
        }

        // Tên nhóm chỉ trả cho cán bộ; giảng viên chỉ thấy nhóm của lớp mình phụ trách.
        var staff = currentUser.IsAdmin || currentUser.Role == UserRole.Lecturer;
        Guid? lecturerId = currentUser.Role == UserRole.Lecturer ? currentUser.UserId : null;
        return await uow.Groups.GetCourseTopicsAsync(courseId, semesterId, classId, staff, lecturerId, ct);
    }

    public async Task<CourseTopicDto> CreateCourseTopicAsync(Guid courseId, CreateCourseTopicRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin && currentUser.Role != UserRole.Lecturer)
            throw new ForbiddenException("Chỉ giảng viên hoặc quản trị viên mới có quyền tạo đề tài mẫu.");

        var courseExists = await uow.Courses.AnyAsync(c => c.Id == courseId, ct);
        if (!courseExists)
            throw new NotFoundException("Môn học không tồn tại.");

        Guid? semesterId;
        if (request.SemesterId is Guid requested)
        {
            semesterId = await RequireSemesterAsync(requested, ct);
        }
        else
        {
            semesterId = (await uow.Semesters.GetCurrentSemesterAsync(ct))?.Id;
        }

        var topic = new CourseTopic
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            SemesterId = semesterId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Requirements = request.Requirements?.Trim(),
            MaxGroups = Math.Max(1, request.MaxGroups),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await uow.Groups.AddCourseTopicAsync(topic, ct);
        await uow.SaveChangesAsync(ct);

        return new CourseTopicDto(
            topic.Id,
            topic.CourseId,
            topic.Title,
            topic.Description,
            topic.Requirements,
            topic.MaxGroups,
            0,
            topic.IsActive,
            topic.CreatedAt,
            topic.SemesterId);
    }

    public async Task<CourseTopicDto> UpdateCourseTopicAsync(Guid topicId, UpdateCourseTopicRequest request, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin && currentUser.Role != UserRole.Lecturer)
            throw new ForbiddenException("Chỉ giảng viên hoặc quản trị viên mới có quyền chỉnh sửa đề tài mẫu.");

        var topic = await uow.Groups.GetCourseTopicByIdAsync(topicId, ct)
            ?? throw new NotFoundException("Đề tài mẫu không tồn tại.");

        topic.Title = request.Title.Trim();
        topic.Description = request.Description?.Trim();
        topic.Requirements = request.Requirements?.Trim();
        topic.MaxGroups = Math.Max(1, request.MaxGroups);
        topic.IsActive = request.IsActive;
        if (request.SemesterId is Guid requested && requested != topic.SemesterId)
        {
            if (topic.Groups.Count > 0)
                throw new ConflictException("Đề tài đã có nhóm chọn nên không chuyển sang kỳ khác được.");
            topic.SemesterId = await RequireSemesterAsync(requested, ct);
        }

        await uow.SaveChangesAsync(ct);

        return new CourseTopicDto(
            topic.Id,
            topic.CourseId,
            topic.Title,
            topic.Description,
            topic.Requirements,
            topic.MaxGroups,
            topic.Groups.Count,
            topic.IsActive,
            topic.CreatedAt,
            topic.SemesterId);
    }

    public async Task DeleteCourseTopicAsync(Guid topicId, CancellationToken ct = default)
    {
        if (!currentUser.IsAdmin && currentUser.Role != UserRole.Lecturer)
            throw new ForbiddenException("Chỉ giảng viên hoặc quản trị viên mới có quyền xóa đề tài mẫu.");

        var topic = await uow.Groups.GetCourseTopicByIdAsync(topicId, ct)
            ?? throw new NotFoundException("Đề tài mẫu không tồn tại.");

        if (topic.Groups.Count > 0)
        {
            topic.IsActive = false;
        }
        else
        {
            uow.Groups.RemoveCourseTopic(topic);
        }

        await uow.SaveChangesAsync(ct);
    }

    public async Task<GroupDetailDto> SelectTopicAsync(Guid groupId, SelectTopicRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        var targetClass = await uow.Classes.GetByIdAsync(group.ClassId, ct)
            ?? throw new NotFoundException("Lớp học không tồn tại.");

        await EnsureLeaderOrStaffAccessAsync(group, ct);

        // Kiểm tra đề tài hiện tại đã duyệt chưa
        var existingProject = await uow.Groups.GetProjectByGroupIdAsync(groupId, ct);
        if (existingProject?.IsApproved == true)
        {
            throw new BadRequestException("Đề tài của nhóm đã được duyệt chính thức. Nếu muốn đổi đề tài, vui lòng gửi yêu cầu thay đổi (Change Request) tới giảng viên.");
        }

        var topic = await uow.Groups.GetCourseTopicByIdAsync(request.TopicId, ct)
            ?? throw new NotFoundException("Đề tài mẫu không tồn tại.");

        if (topic.CourseId != targetClass.CourseId)
            throw new BadRequestException("Đề tài mẫu này không thuộc môn học của lớp.");

        if (topic.SemesterId != null && topic.SemesterId != targetClass.SemesterId)
            throw new BadRequestException("Đề tài mẫu này không mở trong kỳ của lớp.");

        if (!topic.IsActive)
            throw new BadRequestException("Đề tài mẫu này hiện không còn khả dụng để lựa chọn.");

        // Giới hạn tính riêng từng lớp: lớp khác chọn bao nhiêu cũng không ảnh hưởng.
        if (topic.Groups.Count(g => g.ClassId == group.ClassId && g.Id != group.Id) >= topic.MaxGroups)
            throw new ConflictException($"Đề tài '{topic.Title}' đã đủ {topic.MaxGroups} nhóm trong lớp này.");

        group.CourseTopicId = topic.Id;
        group.TopicName = topic.Title;

        var project = await uow.Groups.UpsertProjectForGroupAsync(groupId, targetClass.CourseId, topic.Title, null, ct);

        await uow.Groups.AddAuditEventAsync(
            currentUser.UserId,
            currentUser.Email,
            project.AnalysisRunId,
            "TOPIC_PROPOSED",
            $"Nhóm chọn đề tài mẫu: {topic.Title}",
            ct);

        await uow.SaveChangesAsync(ct);

        return (await uow.Groups.GetGroupDetailAsync(groupId, ct))!;
    }

    public async Task<GroupDetailDto> ProposeTopicAsync(Guid groupId, ProposeTopicRequest request, CancellationToken ct = default)
    {
        var group = await uow.Groups.GetByIdAsync(groupId, ct)
            ?? throw new NotFoundException("Nhóm không tồn tại.");

        var targetClass = await uow.Classes.GetByIdAsync(group.ClassId, ct)
            ?? throw new NotFoundException("Lớp học không tồn tại.");

        await EnsureLeaderOrStaffAccessAsync(group, ct);

        // Kiểm tra đề tài hiện tại đã duyệt chưa
        var existingProject = await uow.Groups.GetProjectByGroupIdAsync(groupId, ct);
        if (existingProject?.IsApproved == true)
        {
            throw new BadRequestException("Đề tài của nhóm đã được duyệt chính thức. Nếu muốn đổi đề tài, vui lòng gửi yêu cầu thay đổi (Change Request) tới giảng viên.");
        }

        var trimmedTopicName = request.TopicName.Trim();
        group.CourseTopicId = null;
        group.TopicName = trimmedTopicName;

        var project = await uow.Groups.UpsertProjectForGroupAsync(groupId, targetClass.CourseId, trimmedTopicName, request.RepoUrl?.Trim(), ct);

        var detailsText = $"Đề xuất đề tài: {trimmedTopicName}";
        if (!string.IsNullOrWhiteSpace(request.Description))
            detailsText += $" - {request.Description.Trim()}";

        await uow.Groups.AddAuditEventAsync(
            currentUser.UserId,
            currentUser.Email,
            project.AnalysisRunId,
            "TOPIC_PROPOSED",
            detailsText,
            ct);

        await uow.SaveChangesAsync(ct);

        return (await uow.Groups.GetGroupDetailAsync(groupId, ct))!;
    }
}
