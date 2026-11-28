using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PACE.API.Controllers;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Hubs;

public record DraftUpdate(long Seq, string Data);

public record DraftCompactionBase(long Since, long UpTo, List<DraftUpdate> Updates);

public record DraftSeed(Guid? TemplateId, string Title, string? Summary, string? ContentJson);

/// <summary>
/// Trả về khi vào bản nháp. State là trạng thái Yjs đã gộp (base64), Updates là các thay đổi sau đó theo thứ tự.
/// Seed chỉ có khi bản nháp vừa được tạo bởi chính lần vào này và nhóm có bản nộp cũ bị trả lại: client này
/// chép nội dung bản cũ vào tài liệu chung, các bạn khác nhận qua Update như bình thường.
/// </summary>
public record DraftJoinResult(string? State, List<DraftUpdate> Updates, long Seq, DraftSeed? Seed);

/// <summary>Kết nối nào đang ở bản nháp nào, với clientID Yjs nào (để báo các bạn khác xoá con trỏ khi rời đi).</summary>
public sealed class TopicDraftPresence
{
    private readonly ConcurrentDictionary<string, (Guid GroupId, long ClientId)> _connections = new();

    public void Join(string connectionId, Guid groupId, long clientId) => _connections[connectionId] = (groupId, clientId);

    public bool IsIn(string connectionId, Guid groupId) =>
        _connections.TryGetValue(connectionId, out var c) && c.GroupId == groupId;

    public (Guid GroupId, long ClientId)? Leave(string connectionId) =>
        _connections.TryRemove(connectionId, out var c) ? c : null;
}

/// <summary>
/// Soạn bản nháp đề xuất cùng lúc. Server chỉ lưu và chuyển tiếp các thay đổi Yjs (dạng nhị phân, gửi base64),
/// không cần hiểu nội dung; Yjs ở client tự gộp nên hai người sửa cùng chỗ không mất chữ của ai.
/// Vị trí con trỏ và ai đang soạn (awareness) chỉ chuyển tiếp, không lưu.
/// Ghi chú: danh sách kết nối giữ trong bộ nhớ, đủ cho một instance. Chạy nhiều instance thì thêm Redis backplane
/// (AddStackExchangeRedis) cho SignalR.
/// </summary>
[Authorize]
public class TopicDraftHub(AppDbContext db, TopicDraftPresence presence, ILogger<TopicDraftHub> logger) : Hub
{
    public const int MaxUpdateBytes = 512 * 1024;
    public const int MaxAwarenessBytes = 32 * 1024;
    public const int MaxStateBytes = 6 * 1024 * 1024;
    /// <summary>Log dài hơn ngần này thì nhờ client gộp lại.</summary>
    public const int CompactAfter = 200;

    public static string GroupName(Guid groupId) => $"topic-draft:{groupId}";

    public async Task<DraftJoinResult> Join(Guid groupId, long clientId)
    {
        var ct = Context.ConnectionAborted;
        var user = await CurrentUser(ct);
        if (!await TopicProposalController.IsMember(db, user, groupId, ct))
            throw new HubException("Bạn không thuộc nhóm này.");
        try { await TopicProposalController.RequireEditable(db, groupId, ct); }
        catch (ConflictException ex) { throw new HubException(ex.Message); }

        // Tạo bản nháp nếu chưa có. Chỉ một lần vào thắng được câu INSERT này, nên chỉ một client chép nội dung cũ.
        var now = DateTime.UtcNow;
        var created = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO group_topic_drafts (group_id, content, revision, updated_by, updated_at, ydoc_seq)
            VALUES ({groupId}, '{{}}'::jsonb, 0, {user.Id}, {now}, 0)
            ON CONFLICT (group_id) DO NOTHING", ct);

        DraftSeed? seed = null;
        if (created == 1)
        {
            seed = await db.GroupTopicSubmissions.AsNoTracking().Where(t => t.GroupId == groupId)
                .OrderByDescending(t => t.Version)
                .Select(t => new DraftSeed(t.TemplateId, t.Title, t.Summary ?? t.Description, t.ContentJson))
                .FirstOrDefaultAsync(ct);
        }

        var draft = await db.GroupTopicDrafts.AsNoTracking().Where(d => d.GroupId == groupId)
            .Select(d => new { d.YDocState, d.YDocSeq }).SingleAsync(ct);
        var updates = await db.GroupTopicDraftUpdates.AsNoTracking()
            .Where(u => u.GroupId == groupId && u.Id > draft.YDocSeq).OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Update }).ToListAsync(ct);

        presence.Join(Context.ConnectionId, groupId, clientId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId), ct);
        // Báo các bạn đang soạn gửi lại vị trí con trỏ cho người mới vào.
        await Clients.OthersInGroup(GroupName(groupId)).SendAsync("PeerJoined", clientId, ct);

        var seq = updates.Count > 0 ? updates[^1].Id : draft.YDocSeq;
        return new DraftJoinResult(
            draft.YDocState is { Length: > 0 } s ? Convert.ToBase64String(s) : null,
            updates.Select(u => new DraftUpdate(u.Id, Convert.ToBase64String(u.Update))).ToList(),
            seq,
            seed);
    }

    /// <summary>Lưu một thay đổi Yjs và phát cho các bạn khác trong nhóm. Trả về số thứ tự của thay đổi.</summary>
    public async Task<long> PushUpdate(Guid groupId, string update)
    {
        var ct = Context.ConnectionAborted;
        RequireJoined(groupId);
        var bytes = Decode(update, MaxUpdateBytes, "Thay đổi quá lớn.");

        var row = new GroupTopicDraftUpdate { GroupId = groupId, Update = bytes, UserId = UserId(), CreatedAt = DateTime.UtcNow };
        db.GroupTopicDraftUpdates.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Thường là bản nháp vừa bị xoá vì nhóm trưởng đã gửi duyệt.
            logger.LogInformation(ex, "Không lưu được thay đổi bản nháp của nhóm {GroupId}", groupId);
            throw new HubException("Bản nháp không còn nữa, có thể nhóm vừa gửi duyệt. Tải lại trang.");
        }

        await Clients.OthersInGroup(GroupName(groupId)).SendAsync("Update", row.Id, update, ct);

        var since = await db.GroupTopicDrafts.AsNoTracking().Where(d => d.GroupId == groupId).Select(d => d.YDocSeq).SingleOrDefaultAsync(ct);
        var pending = await db.GroupTopicDraftUpdates.CountAsync(u => u.GroupId == groupId && u.Id > since, ct);
        if (pending > CompactAfter)
            await Clients.Caller.SendAsync("CompactRequested", ct);
        return row.Id;
    }

    /// <summary>
    /// Chuẩn bị gộp: trả mọi thay đổi chưa gộp để client áp đủ vào tài liệu của mình, và mốc UpTo được phép gộp tới.
    /// Mốc chỉ tính các thay đổi đã cũ hơn vài giây, vì một thay đổi số nhỏ hơn có thể commit sau thay đổi số lớn hơn;
    /// gộp quá sát sẽ xoá mất thay đổi chưa kịp đọc.
    /// </summary>
    public async Task<DraftCompactionBase> CompactionBase(Guid groupId)
    {
        var ct = Context.ConnectionAborted;
        RequireJoined(groupId);
        var since = await db.GroupTopicDrafts.AsNoTracking().Where(d => d.GroupId == groupId).Select(d => (long?)d.YDocSeq).SingleOrDefaultAsync(ct)
            ?? throw new HubException("Bản nháp không còn nữa. Tải lại trang.");
        var settled = DateTime.UtcNow.AddSeconds(-5);
        var rows = await db.GroupTopicDraftUpdates.AsNoTracking().Where(u => u.GroupId == groupId && u.Id > since).OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Update, u.CreatedAt }).ToListAsync(ct);
        var upTo = rows.Where(r => r.CreatedAt < settled).Select(r => r.Id).DefaultIfEmpty(since).Max();
        return new DraftCompactionBase(since, upTo, rows.Select(r => new DraftUpdate(r.Id, Convert.ToBase64String(r.Update))).ToList());
    }

    /// <summary>
    /// Client gửi trạng thái đã gộp, chứa mọi thay đổi tới upToSeq (client lấy đủ bằng CompactionBase trước khi gửi).
    /// Server thay trạng thái và xoá các dòng log đã gộp.
    /// </summary>
    public async Task Compact(Guid groupId, string state, long upToSeq)
    {
        var ct = Context.ConnectionAborted;
        RequireJoined(groupId);
        var bytes = Decode(state, MaxStateBytes, "Bản nháp quá lớn để lưu.");
        var maxSeq = await db.GroupTopicDraftUpdates.Where(u => u.GroupId == groupId).MaxAsync(u => (long?)u.Id, ct) ?? 0;
        if (upToSeq > maxSeq) throw new HubException("Số thứ tự không hợp lệ.");

        // DbContext bật EnableRetryOnFailure nên transaction tự mở phải chạy trong execution strategy.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var changed = await db.GroupTopicDrafts.Where(d => d.GroupId == groupId && d.YDocSeq < upToSeq)
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.YDocState, bytes).SetProperty(d => d.YDocSeq, upToSeq), ct);
            if (changed == 1)
                await db.GroupTopicDraftUpdates.Where(u => u.GroupId == groupId && u.Id <= upToSeq).ExecuteDeleteAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    /// <summary>Con trỏ và ai đang soạn: chỉ chuyển tiếp cho các bạn khác.</summary>
    public async Task PushAwareness(Guid groupId, string update)
    {
        RequireJoined(groupId);
        Decode(update, MaxAwarenessBytes, "Dữ liệu con trỏ quá lớn.");
        await Clients.OthersInGroup(GroupName(groupId)).SendAsync("Awareness", update, Context.ConnectionAborted);
    }

    public async Task Leave(Guid groupId)
    {
        var left = presence.Leave(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(groupId));
        if (left is { } l && l.GroupId == groupId)
            await Clients.Group(GroupName(groupId)).SendAsync("PeerLeft", l.ClientId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (presence.Leave(Context.ConnectionId) is { } l)
            await Clients.Group(GroupName(l.GroupId)).SendAsync("PeerLeft", l.ClientId);
        await base.OnDisconnectedAsync(exception);
    }

    private void RequireJoined(Guid groupId)
    {
        if (!presence.IsIn(Context.ConnectionId, groupId))
            throw new HubException("Chưa vào bản nháp của nhóm này.");
    }

    private Guid UserId() =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private async Task<User> CurrentUser(CancellationToken ct)
    {
        var id = UserId();
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new HubException("Phiên đăng nhập không hợp lệ. Đăng nhập lại.");
    }

    private static byte[] Decode(string base64, int maxBytes, string tooLarge)
    {
        if (string.IsNullOrEmpty(base64)) throw new HubException("Dữ liệu trống.");
        if (base64.Length > (maxBytes / 3 + 1) * 4) throw new HubException(tooLarge);
        try { return Convert.FromBase64String(base64); }
        catch (FormatException) { throw new HubException("Dữ liệu không hợp lệ."); }
    }
}
