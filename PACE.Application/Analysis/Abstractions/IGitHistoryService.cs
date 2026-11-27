namespace PACE.Application.Analysis.Abstractions;

public record GitRevision(string Sha, DateTime CommittedAt);

/// <summary>Tác giả của một dòng, kèm commit đã viết ra dòng đó.</summary>
public record GitLineAuthor(string Email, string Name, string Sha = "", DateTime? At = null, string Summary = "");

/// <summary>Đọc lịch sử của một bản đã clone: lùi về mốc thời gian và xem ai viết từng dòng.</summary>
public interface IGitHistoryService
{
    /// <summary>Chuyển bản clone về commit mới nhất không muộn hơn <paramref name="beforeUtc"/> (null: mới nhất). Trả null nếu không có commit nào.</summary>
    Task<GitRevision?> CheckoutBeforeAsync(IClonedRepository clone, DateTime? beforeUtc, CancellationToken ct = default);

    /// <summary>Tác giả của từng dòng trong file, theo thứ tự dòng.</summary>
    Task<IReadOnlyList<GitLineAuthor>> BlameAsync(IClonedRepository clone, string relativePath, CancellationToken ct = default);
}
