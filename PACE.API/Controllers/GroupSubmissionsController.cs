using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;
using PACE.Infrastructure.Storage;

namespace PACE.API.Controllers;

public class SubmitGroupWork
{
    public Guid? MilestoneId { get; set; }
    [Required, MaxLength(255)] public string Deliverable { get; set; } = "";
    [MaxLength(2000)] public string? Url { get; set; }
    [MaxLength(1000)] public string? Note { get; set; }
    public IFormFile? File { get; set; }
}

public record ReviewGroupWork(
    [Required] string Status,
    [Range(0.0, 10.0)] decimal? Score,
    [MaxLength(4000)] string? Feedback);

public record SubmittedFileView(
    Guid Id, Guid? MilestoneId, string Deliverable, string? FileName, string? ContentType, long? Size, string? Url, string? Note,
    Guid SubmittedBy, string SubmittedByName, DateTime SubmittedAt, bool Late);

/// <summary><c>Outdated</c>: nhóm đã nộp thêm sau lần đánh giá này.</summary>
public record SubmissionReviewView(string Status, decimal? Score, string? Feedback, string ReviewedByName, DateTime ReviewedAt, bool Outdated);

/// <summary>Một mốc của lớp nhìn từ một nhóm. <c>Required</c> lấy từ các đợt đánh giá đã công bố của mốc.</summary>
public record SubmissionSlotView(
    Guid MilestoneId, string Title, string Kind, DateTime DueAt, IReadOnlyList<string> Required,
    IReadOnlyList<SubmittedFileView> Files, SubmissionReviewView? Review);

public record GroupWorkView(bool CanSubmit, bool CanReview, IReadOnlyList<SubmissionSlotView> Slots, IReadOnlyList<SubmittedFileView> Others);

public static class SubmissionRules
{
    public const int FileLimit = 10 * 1024 * 1024;

    // Loại nội dung suy từ đuôi tệp ở server, không tin giá trị trình duyệt gửi. Không nhận tệp chạy được.
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".sql"] = "text/plain",
        [".drawio"] = "application/xml",
        [".xml"] = "application/xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".zip"] = "application/zip",
    };

    public static (string Name, string ContentType) File(string fileName, long length)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/')).Trim();
        if (name.Length is 0 or > 255)
            throw new BadRequestException("The file name must contain 1–255 characters.");
        if (length is <= 0 or > FileLimit)
            throw new BadRequestException("Upload a nonempty file of at most 10 MB.");
        if (!Types.TryGetValue(Path.GetExtension(name), out var type))
            throw new BadRequestException("This file type is not accepted. Use a document, image, spreadsheet, text or zip file.");
        return (name, type);
    }

    /// <summary>Khóa của tệp ở kho ngoài. Không dùng tên tệp người nộp đặt, chỉ giữ đuôi.</summary>
    public static string StorageKey(Guid groupId, Guid id, string fileName) =>
        $"pace/groups/{groupId:N}/{id:N}{Path.GetExtension(fileName).ToLowerInvariant()}";

    public static string Link(string url)
    {
        var value = url.Trim();
        if (value.Length > 2000 || !Uri.TryCreate(value, UriKind.Absolute, out var parsed) || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            throw new BadRequestException("Provide a full http or https link.");
        return value;
    }

    public static SubmissionReviewStatus Review(ReviewGroupWork request)
    {
        var status = request.Status switch
        {
            nameof(SubmissionReviewStatus.Accepted) => SubmissionReviewStatus.Accepted,
            nameof(SubmissionReviewStatus.NeedsRevision) => SubmissionReviewStatus.NeedsRevision,
            _ => throw new BadRequestException("Invalid review status.")
        };
        if (status == SubmissionReviewStatus.NeedsRevision && string.IsNullOrWhiteSpace(request.Feedback))
            throw new BadRequestException("Say what the group needs to change.");
        if (request.Score is decimal score && (score < 0 || score > 10 || decimal.Round(score, 2) != score))
            throw new BadRequestException("The score must be between 0 and 10 with at most two decimals.");
        return status;
    }

    /// <summary>Các sản phẩm phải nộp của một mốc, gộp từ bản công bố mới nhất của mỗi đợt đánh giá.</summary>
    public static List<string> Required(IEnumerable<(Guid RoundId, int Version, string ConfigurationJson)> revisions) =>
        revisions.GroupBy(x => x.RoundId)
            .SelectMany(g => JsonSerializer.Deserialize<AssessmentRoundConfiguration>(g.OrderByDescending(x => x.Version).First().ConfigurationJson)!.Deliverables)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

[ApiController, Authorize, Route("api/groups/{groupId:guid}/submissions")]
public class GroupSubmissionsController(AppDbContext db, TeachingAccess access, IFileStore store, ILogger<GroupSubmissionsController> logger) : ControllerBase
{
    private record Scope(User User, Group Group, bool Reviewer, bool Leader);

    private async Task<Scope> Require(Guid groupId, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await db.Groups.AsNoTracking().Include(x => x.Class).SingleOrDefaultAsync(x => x.Id == groupId, ct)
            ?? throw new NotFoundException("Group unavailable.");
        if (user.Role == UserRole.Admin || (user.Role == UserRole.Lecturer && group.Class.LecturerId == user.Id))
            return new(user, group, true, false);
        if (user.Role == UserRole.Student && user.StudentCode != null)
        {
            var member = await db.GroupMembers.AsNoTracking().SingleOrDefaultAsync(x => x.GroupId == groupId && x.StudentCode == user.StudentCode, ct);
            if (member != null)
                return new(user, group, false, member.Role == "Leader");
        }
        throw new ForbiddenException();
    }

    [HttpGet]
    public async Task<ActionResult<GroupWorkView>> Get(Guid groupId, CancellationToken ct)
    {
        var scope = await Require(groupId, ct);
        var classId = scope.Group.ClassId;
        var milestones = await db.ClassMilestones.AsNoTracking().Where(x => x.ClassId == classId).OrderBy(x => x.DueAt).ThenBy(x => x.Id).ToListAsync(ct);
        var revisions = await db.AssessmentRoundRevisions.AsNoTracking().Where(x => x.Round.Milestone.ClassId == classId)
            .Select(x => new { x.Round.MilestoneId, x.RoundId, x.Version, x.ConfigurationJson }).ToListAsync(ct);
        // Không đọc nội dung tệp khi chỉ liệt kê.
        var files = await db.GroupSubmissions.AsNoTracking().Where(x => x.GroupId == groupId).OrderByDescending(x => x.SubmittedAt)
            .Select(x => new { x.Id, x.MilestoneId, x.Deliverable, x.FileName, x.ContentType, x.FileSize, x.Url, x.Note, x.SubmittedBy, x.SubmittedAt }).ToListAsync(ct);
        var reviews = await db.GroupSubmissionReviews.AsNoTracking().Where(x => x.GroupId == groupId).ToListAsync(ct);
        var people = files.Select(x => x.SubmittedBy).Concat(reviews.Select(x => x.ReviewedBy)).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(x => people.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName ?? x.Email, ct);
        var due = milestones.ToDictionary(x => x.Id, x => x.DueAt);

        var views = files.Select(x => new SubmittedFileView(x.Id, x.MilestoneId, x.Deliverable, x.FileName, x.ContentType, x.FileSize, x.Url, x.Note,
            x.SubmittedBy, names.GetValueOrDefault(x.SubmittedBy, ""), x.SubmittedAt,
            x.MilestoneId is Guid m && due.TryGetValue(m, out var deadline) && x.SubmittedAt > deadline)).ToList();

        var slots = milestones.Select(m =>
        {
            var mine = views.Where(x => x.MilestoneId == m.Id).ToList();
            var review = reviews.SingleOrDefault(x => x.MilestoneId == m.Id);
            return new SubmissionSlotView(m.Id, m.Title, m.Kind.ToString(), m.DueAt,
                SubmissionRules.Required(revisions.Where(x => x.MilestoneId == m.Id).Select(x => (x.RoundId, x.Version, x.ConfigurationJson))),
                mine,
                review == null ? null : new SubmissionReviewView(review.Status.ToString(), review.Score, review.Feedback,
                    names.GetValueOrDefault(review.ReviewedBy, ""), review.ReviewedAt, mine.Any(x => x.SubmittedAt > review.ReviewedAt)));
        }).ToList();

        return Ok(new GroupWorkView(scope.Leader, scope.Reviewer, slots, views.Where(x => x.MilestoneId == null).ToList()));
    }

    [HttpPost, Authorize(Roles = "Student")]
    [RequestSizeLimit(SubmissionRules.FileLimit + 65536), RequestFormLimits(MultipartBodyLengthLimit = SubmissionRules.FileLimit + 65536)]
    public async Task<IActionResult> Submit(Guid groupId, [FromForm] SubmitGroupWork request, CancellationToken ct)
    {
        var scope = await Require(groupId, ct);
        if (!scope.Leader)
            throw new ForbiddenException("Only the group leader can submit the group's work.");
        if (string.IsNullOrWhiteSpace(request.Deliverable))
            throw new BadRequestException("Say which deliverable this is.");
        var hasLink = !string.IsNullOrWhiteSpace(request.Url);
        if (hasLink == (request.File != null))
            throw new BadRequestException("Send either a file or a link.");
        if (request.MilestoneId is Guid milestoneId && !await db.ClassMilestones.AnyAsync(x => x.Id == milestoneId && x.ClassId == scope.Group.ClassId, ct))
            throw new NotFoundException("Schedule item unavailable.");

        var item = new GroupSubmission
        {
            GroupId = groupId,
            MilestoneId = request.MilestoneId,
            Deliverable = request.Deliverable.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            SubmittedBy = scope.User.Id,
            SubmittedAt = DateTime.UtcNow
        };
        if (request.File is IFormFile file)
        {
            (item.FileName, item.ContentType) = SubmissionRules.File(file.FileName, file.Length);
            item.FileSize = file.Length;
            using var buffer = new MemoryStream((int)file.Length);
            await file.CopyToAsync(buffer, ct);
            var content = buffer.ToArray();
            if (store.Enabled)
            {
                item.StorageKey = SubmissionRules.StorageKey(groupId, item.Id, item.FileName);
                await store.UploadAsync(item.StorageKey, content, item.FileName, ct);
            }
            else
                item.Content = content;
        }
        else
            item.Url = SubmissionRules.Link(request.Url!);

        db.GroupSubmissions.Add(item);
        access.Audit(item.Id, "GROUP_WORK_SUBMITTED", item.Deliverable);
        try
        {
            await access.Save(ct);
        }
        catch when (item.StorageKey != null)
        {
            // Không ghi được vào database thì tệp vừa đẩy lên thành tệp thừa, gỡ đi.
            await Discard(item.StorageKey);
            throw;
        }
        return CreatedAtAction(nameof(Get), new { groupId }, new { item.Id });
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Content(Guid groupId, Guid id, CancellationToken ct)
    {
        await Require(groupId, ct);
        var item = await db.GroupSubmissions.AsNoTracking().Where(x => x.Id == id && x.GroupId == groupId)
            .Select(x => new { x.Content, x.StorageKey, x.ContentType, x.FileName }).SingleOrDefaultAsync(ct);
        var content = item?.StorageKey != null ? await store.DownloadAsync(item.StorageKey, ct) : item?.Content;
        if (content == null)
            throw new NotFoundException("File unavailable.");
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(content, item!.ContentType ?? "application/octet-stream", item.FileName);
    }

    /// <summary>Nhóm trưởng gỡ một lần nộp nhầm, khi mốc chưa tới hạn và giảng viên chưa xem bản đó.</summary>
    [HttpDelete("{id:guid}"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Delete(Guid groupId, Guid id, CancellationToken ct)
    {
        var scope = await Require(groupId, ct);
        if (!scope.Leader)
            throw new ForbiddenException("Only the group leader can remove the group's work.");
        var item = await db.GroupSubmissions.Include(x => x.Milestone).SingleOrDefaultAsync(x => x.Id == id && x.GroupId == groupId, ct)
            ?? throw new NotFoundException("File unavailable.");
        if (item.Milestone != null)
        {
            if (item.Milestone.DueAt < DateTime.UtcNow)
                throw new ConflictException("The deadline has passed. Submit a newer version instead.");
            if (await db.GroupSubmissionReviews.AnyAsync(x => x.GroupId == groupId && x.MilestoneId == item.MilestoneId && x.ReviewedAt >= item.SubmittedAt, ct))
                throw new ConflictException("The lecturer has already reviewed this version. Submit a newer version instead.");
        }
        db.GroupSubmissions.Remove(item);
        access.Audit(item.Id, "GROUP_WORK_REMOVED", item.Deliverable);
        await access.Save(ct);
        if (item.StorageKey != null)
            await Discard(item.StorageKey);
        return NoContent();
    }

    /// <summary>Gỡ tệp ở kho ngoài. Hỏng thì chỉ ghi log: dòng trong database mới quyết định người dùng thấy gì.</summary>
    private async Task Discard(string key)
    {
        try
        {
            await store.DeleteAsync(key, CancellationToken.None);
        }
        catch (ExternalServiceException)
        {
            logger.LogWarning("Left an unused file in storage: {Key}", key);
        }
    }

    [HttpPut("review/{milestoneId:guid}"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Review(Guid groupId, Guid milestoneId, ReviewGroupWork request, CancellationToken ct)
    {
        var scope = await Require(groupId, ct);
        if (!scope.Reviewer)
            throw new ForbiddenException();
        var status = SubmissionRules.Review(request);
        if (!await db.ClassMilestones.AnyAsync(x => x.Id == milestoneId && x.ClassId == scope.Group.ClassId, ct))
            throw new NotFoundException("Schedule item unavailable.");
        if (!await db.GroupSubmissions.AnyAsync(x => x.GroupId == groupId && x.MilestoneId == milestoneId, ct))
            throw new ConflictException("The group has not submitted anything for this item.");

        var review = await db.GroupSubmissionReviews.SingleOrDefaultAsync(x => x.GroupId == groupId && x.MilestoneId == milestoneId, ct);
        if (review == null)
            db.GroupSubmissionReviews.Add(review = new GroupSubmissionReview { GroupId = groupId, MilestoneId = milestoneId });
        review.Status = status;
        review.Score = request.Score;
        review.Feedback = string.IsNullOrWhiteSpace(request.Feedback) ? null : request.Feedback.Trim();
        review.ReviewedBy = scope.User.Id;
        review.ReviewedAt = DateTime.UtcNow;
        access.Audit(groupId, "GROUP_WORK_REVIEWED", $"{milestoneId}: {status}");
        await access.Save(ct);
        return NoContent();
    }
}
