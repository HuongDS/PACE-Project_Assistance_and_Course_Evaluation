using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PACE.API.Hubs;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record ProposalTemplateView(
    Guid Id, string Name, string? Description, int Version, string Scope, List<ProposalSection> Sections);

public record TopicDraftView(
    Guid? TemplateId, string? Title, string? Summary, string ContentJson, string? NoteToLecturer,
    int Revision, DateTime UpdatedAt, string? UpdatedByName);

public record SaveTopicDraft(
    Guid? TemplateId,
    [MaxLength(TopicProposalRules.TitleMax)] string? Title,
    [MaxLength(TopicProposalRules.SummaryMax)] string? Summary,
    [Required] string ContentJson,
    [MaxLength(TopicProposalRules.NoteMax)] string? NoteToLecturer);

public record TopicAttachmentView(Guid Id, string FileName, string? ContentType, long FileSize, DateTime UploadedAt);

public record TopicProposalDetail(
    Guid GroupId, int Version, string Title, string? Summary, string Status, string ProposalSource,
    Guid? TemplateId, int? TemplateVersion, string? ContentJson, string? ContentHtml, string? NoteToLecturer,
    string? ProposalFileName, long? ProposalFileSize, List<TopicAttachmentView> Attachments);

public class SubmitTopicProposalForm
{
    [Required(ErrorMessage = "Tên đề tài không được để trống.")]
    [MaxLength(TopicProposalRules.TitleMax, ErrorMessage = "Tên đề tài tối đa 255 ký tự.")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Cần tóm tắt một câu.")]
    [MaxLength(TopicProposalRules.SummaryMax, ErrorMessage = "Tóm tắt tối đa 300 ký tự.")]
    public string Summary { get; set; } = null!;

    [Required(ErrorMessage = "Chưa chọn khung đề xuất.")]
    public Guid TemplateId { get; set; }

    [Range(0, int.MaxValue - 1)]
    public int PreviousVersion { get; set; }

    /// <summary>{"sections":{"key":{doc Tiptap}},"team":{"MSSV":"phần việc"}}</summary>
    [Required(ErrorMessage = "Thiếu nội dung đề xuất.")]
    public string ContentJson { get; set; } = null!;

    [MaxLength(TopicProposalRules.NoteMax, ErrorMessage = "Lời nhắn tối đa 1000 ký tự.")]
    public string? NoteToLecturer { get; set; }

    /// <summary>PDF do trình duyệt dựng từ nội dung đang soạn, giống hệt bản nhóm đã xem ở bước kiểm tra.</summary>
    public IFormFile? Proposal { get; set; }

    public List<IFormFile>? Attachments { get; set; }
}

/// <summary>
/// Đề xuất đề tài theo khung: danh sách khung của nhóm, bản chiếu của bản nháp đang soạn chung,
/// gửi duyệt (PDF + nội dung có cấu trúc + tài liệu kèm theo) và xem chi tiết bản đã nộp.
/// Phần soạn cùng lúc chạy qua TopicDraftHub; các endpoint ở đây không đụng tới dữ liệu Yjs.
/// </summary>
[ApiController, Authorize]
public class TopicProposalController(AppDbContext db, TeachingAccess access, IHubContext<TopicDraftHub> hub) : ControllerBase
{
    [HttpGet("api/groups/{groupId:guid}/proposal-templates")]
    public async Task<IActionResult> Templates(Guid groupId, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        await RequireMemberOrReviewer(user, group, ct);
        return Ok(await VisibleTemplates(group, ct));
    }

    [HttpGet("api/groups/{groupId:guid}/topic-draft")]
    public async Task<IActionResult> Draft(Guid groupId, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        await RequireMemberOrReviewer(user, group, ct);

        var draft = await db.GroupTopicDrafts.AsNoTracking().Where(d => d.GroupId == groupId)
            .Select(d => new { d.TemplateId, d.Title, d.Summary, d.ContentJson, d.NoteToLecturer, d.Revision, d.UpdatedAt, d.UpdatedBy })
            .SingleOrDefaultAsync(ct);
        if (draft is null) return NoContent();
        var name = await db.Users.Where(u => u.Id == draft.UpdatedBy).Select(u => u.FullName).SingleOrDefaultAsync(ct);
        return Ok(new TopicDraftView(draft.TemplateId, draft.Title, draft.Summary, draft.ContentJson, draft.NoteToLecturer,
            draft.Revision, draft.UpdatedAt, name));
    }

    /// <summary>
    /// Lưu bản chiếu của bản nháp (client gọi vài giây một lần khi có thay đổi) để server xem trước và liệt kê
    /// mà không cần đọc Yjs. Mọi thành viên đều có thể gọi; nội dung của các bên đã hội tụ nên ghi sau thắng.
    /// </summary>
    [HttpPut("api/groups/{groupId:guid}/topic-draft")]
    [Authorize(Roles = "Student")]
    [RequestSizeLimit(TopicProposalRules.ContentMaxBytes + 64 * 1024)]
    public async Task<IActionResult> SaveDraft(Guid groupId, SaveTopicDraft request, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        await RequireMember(user, groupId, ct);
        await RequireEditable(groupId, ct);

        if (System.Text.Encoding.UTF8.GetByteCount(request.ContentJson) > TopicProposalRules.ContentMaxBytes)
            throw new BadRequestException("Nội dung bản nháp quá dài (tối đa 1 MB).");
        try { using var _ = JsonDocument.Parse(request.ContentJson); }
        catch (JsonException) { throw new BadRequestException("Nội dung bản nháp không đúng định dạng."); }
        if (request.TemplateId is Guid templateId && !(await VisibleTemplates(group, ct)).Any(t => t.Id == templateId))
            throw new BadRequestException("Khung đề xuất không dùng được cho nhóm này.");

        var draft = await db.GroupTopicDrafts.SingleOrDefaultAsync(d => d.GroupId == groupId, ct);
        if (draft is null)
        {
            draft = new GroupTopicDraft { GroupId = groupId };
            db.GroupTopicDrafts.Add(draft);
        }
        draft.TemplateId = request.TemplateId;
        draft.Title = request.Title?.Trim();
        draft.Summary = request.Summary?.Trim();
        draft.ContentJson = request.ContentJson;
        draft.NoteToLecturer = request.NoteToLecturer?.Trim();
        draft.Revision++;
        draft.UpdatedBy = user.Id;
        draft.UpdatedAt = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Một bạn khác vừa lưu cùng lúc; bản của họ chứa cùng nội dung đã hội tụ nên bỏ qua lần lưu này.
            return Accepted();
        }
        return NoContent();
    }

    [HttpPost("api/groups/{groupId:guid}/topics/proposal")]
    [Authorize(Roles = "Student")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(TopicProposalRules.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = TopicProposalRules.MaxRequestBytes)]
    public async Task<IActionResult> Submit(Guid groupId, [FromForm] SubmitTopicProposalForm form, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        await RequireMember(user, groupId, ct);

        var latest = await db.GroupTopicSubmissions.AsNoTracking().Where(t => t.GroupId == groupId)
            .OrderByDescending(t => t.Version).FirstOrDefaultAsync(ct);
        if ((latest?.Version ?? 0) != form.PreviousVersion)
            throw new ConflictException("Nhóm vừa có bản nộp mới hơn. Tải lại trang rồi thử lại.");
        if (latest != null && latest.Status is not (GroupTopicStatus.ChangesRequested or GroupTopicStatus.Rejected))
            throw new ConflictException("Bản trước phải được giảng viên trả lại hoặc từ chối thì mới nộp lại được.");

        var (title, summary, note) = TopicProposalRules.CheckText(form.Title, form.Summary, form.NoteToLecturer);
        var template = (await VisibleTemplates(group, ct)).FirstOrDefault(t => t.Id == form.TemplateId)
            ?? throw new BadRequestException("Khung đề xuất không dùng được cho nhóm này.");
        var members = await Members(groupId, ct);
        var content = TopicProposalRules.BuildContent(form.ContentJson, template.Id, template.Version, template.Sections, members);
        if (content.Problems.Count > 0)
            throw new BadRequestException("Bản đề xuất chưa đủ: " + string.Join(" ", content.Problems));

        var version = form.PreviousVersion + 1;
        var pdf = await TopicProposalRules.ReadGeneratedPdfAsync(form.Proposal, TopicProposalRules.PdfFileName(group.Name, version), ct);
        var attachments = await TopicProposalRules.ReadAttachmentsAsync(form.Attachments, ct);

        var topic = new GroupTopicSubmission
        {
            Group = group,
            GroupId = groupId,
            Version = version,
            Title = title,
            // Các màn hình cũ đọc Description; giữ câu tóm tắt ở đó để chúng vẫn hiện đúng.
            Description = summary,
            Summary = summary,
            Status = GroupTopicStatus.Pending,
            SubmittedBy = user.Id,
            SubmittedAt = DateTime.UtcNow,
            TemplateId = template.Id,
            TemplateVersion = template.Version,
            ContentJson = content.Json,
            ContentHtml = content.Html,
            NoteToLecturer = note,
            ProposalSource = TopicProposalSource.Generated,
            ProposalFileName = pdf.FileName,
            ProposalContentType = pdf.ContentType,
            ProposalFileSize = pdf.Content.LongLength,
            ProposalFile = pdf.Content
        };
        foreach (var a in attachments)
            topic.Attachments.Add(new GroupTopicAttachment
            {
                GroupId = groupId, Version = version, FileName = a.FileName, ContentType = a.ContentType,
                FileSize = a.Content.LongLength, Content = a.Content, UploadedBy = user.Id
            });
        db.GroupTopicSubmissions.Add(topic);
        group.TopicName = title;

        await access.Save(ct);

        // Bản nộp đã lưu thì bản nháp và log Yjs không còn dùng nữa (DB tự xoá dây chuyền log). Xoá sau khi lưu,
        // để nếu lưu bản nộp lỗi thì bản nháp vẫn còn; và không dùng khoá revision, để lần tự lưu của bạn cùng nhóm
        // xảy ra đúng lúc này không làm hỏng việc gửi.
        await db.GroupTopicDrafts.Where(d => d.GroupId == groupId).ExecuteDeleteAsync(ct);

        await hub.Clients.Group(TopicDraftHub.GroupName(groupId)).SendAsync("Submitted", version, user.FullName, ct);

        return CreatedAtAction(nameof(Detail), new { groupId, version }, new GroupTopicView(
            groupId, group.Name, topic.Version, topic.Title, topic.Description, topic.Status.ToString(), topic.SubmittedBy,
            topic.SubmittedAt, null, null, null, topic.ProposalFileName, topic.ProposalFileSize));
    }

    [HttpGet("api/groups/{groupId:guid}/topics/{version:int}/detail")]
    public async Task<IActionResult> Detail(Guid groupId, int version, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        await RequireMemberOrReviewer(user, group, ct);

        var topic = await db.GroupTopicSubmissions.AsNoTracking()
            .Where(t => t.GroupId == groupId && t.Version == version)
            .Select(t => new TopicProposalDetail(t.GroupId, t.Version, t.Title, t.Summary ?? t.Description, t.Status.ToString(),
                t.ProposalSource.ToString(), t.TemplateId, t.TemplateVersion, t.ContentJson, t.ContentHtml, t.NoteToLecturer,
                t.ProposalFileName, t.ProposalFileSize,
                t.Attachments.OrderBy(a => a.UploadedAt).ThenBy(a => a.FileName)
                    .Select(a => new TopicAttachmentView(a.Id, a.FileName, a.ContentType, a.FileSize, a.UploadedAt)).ToList()))
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy phiên bản đề tài này.");
        return Ok(topic);
    }

    [HttpGet("api/groups/{groupId:guid}/topics/{version:int}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid groupId, int version, Guid attachmentId, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        await RequireMemberOrReviewer(user, group, ct);

        var file = await db.GroupTopicAttachments.AsNoTracking()
            .Where(a => a.Id == attachmentId && a.GroupId == groupId && a.Version == version)
            .Select(a => new { a.Content, a.ContentType, a.FileName })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy tài liệu kèm theo.");
        return File(file.Content, file.ContentType ?? "application/octet-stream", file.FileName);
    }

    // ---------- Dùng chung (cả TopicDraftHub) ----------

    /// <summary>Khung nhóm được dùng: của lớp, của môn, rồi khung chung; khung lớp đứng đầu danh sách.</summary>
    internal static async Task<List<ProposalTemplateView>> VisibleTemplates(AppDbContext db, Group group, CancellationToken ct)
    {
        var classId = group.ClassId;
        var courseId = group.Class.CourseId;
        var rows = await db.ProposalTemplates.AsNoTracking()
            .Where(t => t.IsActive && (t.ClassId == classId || (t.ClassId == null && (t.CourseId == courseId || t.CourseId == null))))
            .Select(t => new { t.Id, t.Name, t.Description, t.Version, t.ClassId, t.CourseId, t.SortOrder, t.SectionsJson })
            .ToListAsync(ct);
        return rows
            .Select(t => new
            {
                Row = t,
                Rank = t.ClassId != null ? 0 : t.CourseId != null ? 1 : 2
            })
            .OrderBy(t => t.Rank).ThenBy(t => t.Row.SortOrder).ThenBy(t => t.Row.Name)
            .Select(t => new ProposalTemplateView(t.Row.Id, t.Row.Name, t.Row.Description, t.Row.Version,
                t.Rank switch { 0 => "class", 1 => "course", _ => "global" },
                TopicProposalRules.ParseSections(t.Row.SectionsJson)))
            .Where(t => t.Sections.Count > 0)
            .ToList();
    }

    /// <summary>Bản nháp chỉ sửa được khi nhóm chưa có bản nộp, hoặc bản mới nhất đã bị trả lại/từ chối.</summary>
    internal static async Task RequireEditable(AppDbContext db, Guid groupId, CancellationToken ct)
    {
        var status = await db.GroupTopicSubmissions.AsNoTracking().Where(t => t.GroupId == groupId)
            .OrderByDescending(t => t.Version).Select(t => (GroupTopicStatus?)t.Status).FirstOrDefaultAsync(ct);
        if (status == GroupTopicStatus.Pending)
            throw new ConflictException("Đề tài đang chờ giảng viên duyệt nên không sửa bản đề xuất được.");
        if (status == GroupTopicStatus.Approved)
            throw new ConflictException("Đề tài đã được duyệt.");
    }

    internal static async Task<bool> IsMember(AppDbContext db, User user, Guid groupId, CancellationToken ct) =>
        user.Role == UserRole.Student && user.StudentCode != null &&
        await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.StudentCode == user.StudentCode, ct);

    private Task<List<ProposalTemplateView>> VisibleTemplates(Group group, CancellationToken ct) => VisibleTemplates(db, group, ct);

    private Task RequireEditable(Guid groupId, CancellationToken ct) => RequireEditable(db, groupId, ct);

    private async Task<List<ProposalMember>> Members(Guid groupId, CancellationToken ct) =>
        (await db.GroupMembers.AsNoTracking().Where(m => m.GroupId == groupId)
            .Select(m => new { m.StudentCode, m.Role, m.JoinedAt, Name = m.StudentProfile.User.FullName })
            .ToListAsync(ct))
        .OrderBy(m => m.Role.Equals("Leader", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(m => m.JoinedAt)
        .Select(m => new ProposalMember(m.StudentCode, string.IsNullOrWhiteSpace(m.Name) ? null : m.Name, m.Role))
        .ToList();

    private async Task<Group> LoadGroup(Guid id, CancellationToken ct) =>
        await db.Groups.Include(g => g.Class).SingleOrDefaultAsync(g => g.Id == id, ct)
        ?? throw new NotFoundException("Không tìm thấy nhóm.");

    private async Task RequireMember(User user, Guid groupId, CancellationToken ct)
    {
        if (!await IsMember(db, user, groupId, ct)) throw new ForbiddenException();
    }

    private async Task RequireMemberOrReviewer(User user, Group group, CancellationToken ct)
    {
        if (user.Role == UserRole.Student) await RequireMember(user, group.Id, ct);
        else GroupTopicsController.RequireReviewer(user, group.Class);
    }
}
