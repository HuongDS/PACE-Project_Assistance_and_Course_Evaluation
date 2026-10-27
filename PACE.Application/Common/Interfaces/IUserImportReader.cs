namespace PACE.Application.Common.Interfaces;

public sealed record UserImportRow(int Row, string? Email, string? RepoUrl, string? RepoName, string? StudentCode = null);

public interface IUserImportReader
{
    IReadOnlyList<UserImportRow> Read(Stream excelStream);
}
