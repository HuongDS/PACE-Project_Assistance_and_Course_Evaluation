namespace PACE.Application.Common.Interfaces;

public sealed record RepoLinkRow(int Row, string RepoUrl);

public interface IRepoLinkReader
{
    /// <summary>Đọc danh sách link repo từ file Excel (.xlsx); bỏ qua dòng trống.</summary>
    IReadOnlyList<RepoLinkRow> Read(Stream excelStream);
}
