using ClosedXML.Excel;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;

namespace PACE.Infrastructure.Excel;

public class ClosedXmlClassExcelService : IClassExcelService
{
    public IReadOnlyList<ClassStudentImportRow> ReadStudentList(Stream excelStream)
    {
        ExcelUploadGuard.Validate(excelStream);
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(excelStream);
        }
        catch (Exception ex)
        {
            throw new BadRequestException("File Excel không đọc được (cần định dạng .xlsx).", ex);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new BadRequestException("File Excel không có sheet nào.");
            var header = sheet.FirstRowUsed() ?? throw new BadRequestException("File Excel trống.");

            int? codeCol = null, nameCol = null, emailCol = null;
            foreach (var cell in header.CellsUsed())
            {
                var title = cell.GetString().Trim().ToLowerInvariant();
                if (codeCol is null && (title.Contains("mssv") || title.Contains("mã số") || title.Contains("mã sv") || title.Contains("student code") || title.Contains("studentcode")))
                    codeCol = cell.Address.ColumnNumber;
                else if (nameCol is null && (title.Contains("họ và tên") || title.Contains("họ tên") || title.Contains("tên sv") || title.Contains("full name") || title.Contains("fullname")))
                    nameCol = cell.Address.ColumnNumber;
                else if (emailCol is null && (title.Contains("email") || title.Contains("mail")))
                    emailCol = cell.Address.ColumnNumber;
            }

            if (codeCol is null)
                throw new BadRequestException("Dòng tiêu đề cần có cột Mã sinh viên (MSSV).");

            var result = new List<ClassStudentImportRow>();
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
            {
                var code = row.Cell(codeCol.Value).GetString().Trim();
                if (string.IsNullOrEmpty(code)) continue;

                var name = nameCol.HasValue ? row.Cell(nameCol.Value).GetString().Trim() : null;
                var email = emailCol.HasValue ? row.Cell(emailCol.Value).GetString().Trim() : null;

                result.Add(new ClassStudentImportRow(
                    row.RowNumber(),
                    code,
                    string.IsNullOrEmpty(name) ? null : name,
                    string.IsNullOrEmpty(email) ? null : email));
            }

            return result;
        }
    }

    public byte[] ExportStudentList(string className, IReadOnlyList<ClassStudentDto> students)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Danh sách sinh viên");
        sheet.ShowGridLines = true;

        // Title banner
        var titleRange = sheet.Range("A1:F1");
        titleRange.Merge();
        titleRange.Value = $"DANH SÁCH SINH VIÊN - LỚP {className.ToUpperInvariant()}";
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 13;
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Fill.BackgroundColor = XLColor.FromArgb(185, 71, 32); // Brand Accent
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(1).Height = 32;

        // Headers
        var headers = new[] { "STT", "Mã số sinh viên", "Họ và tên", "Email", "Nhóm đề tài", "Trạng thái" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(2, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f6");
            cell.Style.Alignment.Horizontal = i == 0 ? XLAlignmentHorizontalValues.Center : XLAlignmentHorizontalValues.Left;
        }
        sheet.Row(2).Height = 24;

        // Data rows
        int r = 3;
        for (int i = 0; i < students.Count; i++)
        {
            var s = students[i];
            sheet.Cell(r, 1).Value = i + 1;
            sheet.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            sheet.Cell(r, 2).Value = s.StudentCode;
            sheet.Cell(r, 2).Style.Font.Bold = true;

            sheet.Cell(r, 3).Value = s.FullName ?? "—";
            sheet.Cell(r, 4).Value = s.Email ?? "—";
            sheet.Cell(r, 5).Value = s.GroupName ?? "Chưa phân nhóm";
            sheet.Cell(r, 6).Value = s.Status == 1 ? "Đang học" : "Đã thôi học";

            sheet.Row(r).Height = 20;
            r++;
        }

        // Borders
        var dataRange = sheet.Range(2, 1, Math.Max(2, r - 1), 6);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#d1d5db");
        dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#9ca3af");

        sheet.Columns(1, 6).AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

