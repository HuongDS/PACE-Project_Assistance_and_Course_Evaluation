using ClosedXML.Excel;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;

namespace PACE.Infrastructure.Excel;

public sealed class ClosedXmlRepoLinkReader : IRepoLinkReader
{
    private static readonly string[] HeaderKeywords = ["repo", "github", "link", "url"];

    public IReadOnlyList<RepoLinkRow> Read(Stream stream)
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
            var first = sheet.FirstRowUsed() ?? throw new BadRequestException("File Excel trống.");

            var column = first.CellsUsed()
                .FirstOrDefault(c => IsHeader(c.GetString().Trim()))
                ?.Address.ColumnNumber;
            var lastHeaderRow = first.RowNumber();

            // Không có dòng tiêu đề: chấp nhận file chỉ gồm danh sách link, dòng đầu đã là dữ liệu.
            if (column is null)
            {
                column = first.CellsUsed().FirstOrDefault(c => LooksLikeUrl(CellText(c)))?.Address.ColumnNumber;
                lastHeaderRow = first.RowNumber() - 1;
            }

            if (column is null)
                throw new BadRequestException("Dòng tiêu đề phải có cột Repo (link GitHub), hoặc file chỉ gồm danh sách link.");

            var result = new List<RepoLinkRow>();
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > lastHeaderRow))
            {
                var url = CellText(row.Cell(column.Value));
                if (url.Length > 0) result.Add(new RepoLinkRow(row.RowNumber(), url));
            }

            return result;
        }
    }

    // Ô chứa hyperlink thường hiển thị tên repo, còn địa chỉ thật nằm trong hyperlink.
    private static string CellText(IXLCell cell)
    {
        var text = cell.GetString().Trim();
        if (LooksLikeUrl(text) || !cell.HasHyperlink) return text;
        return cell.GetHyperlink().ExternalAddress?.ToString().Trim() ?? text;
    }

    // Một link như "https://github.com/..." cũng chứa chữ "github" nhưng là dữ liệu, không phải tiêu đề.
    private static bool IsHeader(string text)
        => !LooksLikeUrl(text) && HeaderKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeUrl(string text)
        => text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
