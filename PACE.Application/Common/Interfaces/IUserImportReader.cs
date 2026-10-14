namespace PACE.Application.Common.Interfaces;

public sealed record UserImportRow(int Row, string? Email, string? RepoUrl, string? RepoName);

public interface IUserImportReader
{
    IReadOnlyList<UserImportRow> Read(Stream excelStream);
}
