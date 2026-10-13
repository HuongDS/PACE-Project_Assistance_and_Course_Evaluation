using ClosedXML.Excel;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;

namespace PACE.Infrastructure.Excel;

public sealed class ClosedXmlUserImportReader : IUserImportReader
{
    public IReadOnlyList<UserImportRow> Read(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new BadRequestException("File Excel không đọc được (cần định dạng .xlsx).", ex);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new BadRequestException("File Excel không có sheet nào.");
            var header = sheet.FirstRowUsed() ?? throw new BadRequestException("File Excel trống.");

            int? emailCol = null, repoCol = null, nameCol = null;
            foreach (var cell in header.CellsUsed())
            {
                var title = cell.GetString().Trim().ToLowerInvariant();
                if (emailCol is null && (title.Contains("email") || title.Contains("mail"))) emailCol = cell.Address.ColumnNumber;
                else if (repoCol is null && (title.Contains("repo") || title.Contains("github") || title.Contains("link") || title.Contains("url"))) repoCol = cell.Address.ColumnNumber;
                else if (nameCol is null && (title.Contains("nhóm") || title.Contains("group") || title.Contains("name") || title.Contains("tên"))) nameCol = cell.Address.ColumnNumber;
            }

            if (emailCol is null || repoCol is null)
                throw new BadRequestException("Dòng tiêu đề phải có cột Email và cột Repo (link GitHub).");

            var result = new List<UserImportRow>();
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
            {
                var email = row.Cell(emailCol.Value).GetString().Trim();
                var repo = row.Cell(repoCol.Value).GetString().Trim();
                if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(repo)) continue;
                var name = nameCol.HasValue ? row.Cell(nameCol.Value).GetString().Trim() : null;
                result.Add(new UserImportRow(row.RowNumber(), email, repo, name));
            }

            return result;
        }
    }
}
