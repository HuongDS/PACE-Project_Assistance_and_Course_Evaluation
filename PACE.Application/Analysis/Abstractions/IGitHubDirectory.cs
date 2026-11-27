namespace PACE.Application.Analysis.Abstractions;

/// <summary>Hỏi GitHub về tài khoản. Chỉ để đỡ thao tác: không hỏi được thì hệ thống vẫn chạy.</summary>
public interface IGitHubDirectory
{
    /// <summary>true: có tài khoản này; false: chắc chắn không có; null: không hỏi được.</summary>
    Task<bool?> UserExistsAsync(string username, CancellationToken ct = default);

    /// <summary>Tài khoản GitHub của từng commit, theo mã commit. Commit không nhận ra thì không có trong kết quả.</summary>
    Task<IReadOnlyDictionary<string, string>> CommitLoginsAsync(string repoUrl, IReadOnlyList<string> shas, CancellationToken ct = default);
}
