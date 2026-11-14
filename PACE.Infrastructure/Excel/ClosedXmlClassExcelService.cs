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

            IXLRow? header = null;
            int? codeCol = null, nameCol = null, emailCol = null;

            foreach (var row in sheet.RowsUsed().Take(15))
            {
                int? testCode = null, testName = null, testEmail = null;
                foreach (var cell in row.CellsUsed())
                {
                    var title = cell.GetString().Trim().ToLowerInvariant();
                    if (testCode is null && (title.Contains("mssv") || title.Contains("mã số") || title.Contains("mã sv") || title.Contains("student code") || title.Contains("studentcode")))
                        testCode = cell.Address.ColumnNumber;
                    else if (testName is null && (title.Contains("họ và tên") || title.Contains("họ tên") || title.Contains("tên sv") || title.Contains("full name") || title.Contains("fullname")))
                        testName = cell.Address.ColumnNumber;
                    else if (testEmail is null && (title.Contains("email") || title.Contains("mail")))
                        testEmail = cell.Address.ColumnNumber;
                }

                if (testCode.HasValue)
                {
                    header = row;
                    codeCol = testCode;
                    nameCol = testName;
                    emailCol = testEmail;
                    break;
                }
            }

            if (header is null || codeCol is null)
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

    public IReadOnlyList<ClassImportRow> ReadClassList(Stream excelStream)
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

            IXLRow? header = null;
            int? nameCol = null, codeCol = null, startCol = null, endCol = null;

            foreach (var row in sheet.RowsUsed().Take(15))
            {
                int? testName = null, testCode = null, testStart = null, testEnd = null;
                foreach (var cell in row.CellsUsed())
                {
                    var title = cell.GetString().Trim().ToLowerInvariant();
                    if (testName is null && (title.Contains("tên lớp") || title.Contains("class name") || title.Contains("classname") || title.Contains("tên lop") || title.Equals("class") || title.Equals("lớp")))
                        testName = cell.Address.ColumnNumber;
                    else if (testCode is null && (title.Contains("mã môn") || title.Contains("course code") || title.Contains("coursecode") || title.Contains("mã mon") || title.Contains("môn học") || title.Contains("subject") || title.Equals("course") || title.Equals("môn")))
                        testCode = cell.Address.ColumnNumber;
                    else if (testStart is null && (title.Contains("bắt đầu") || title.Contains("start date") || title.Contains("startdate") || title.Contains("start")))
                        testStart = cell.Address.ColumnNumber;
                    else if (testEnd is null && (title.Contains("kết thúc") || title.Contains("end date") || title.Contains("enddate") || title.Contains("end")))
                        testEnd = cell.Address.ColumnNumber;
                }

                if (testName.HasValue && testCode.HasValue)
                {
                    header = row;
                    nameCol = testName;
                    codeCol = testCode;
                    startCol = testStart;
                    endCol = testEnd;
                    break;
                }
            }

            if (header is null || nameCol is null || codeCol is null)
                throw new BadRequestException("Dòng tiêu đề file Excel cần có ít nhất 2 cột: 'Tên lớp' và 'Mã môn học'.");

            var result = new List<ClassImportRow>();
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
            {
                var className = row.Cell(nameCol.Value).GetString().Trim();
                if (string.IsNullOrEmpty(className)) continue;

                var courseCode = row.Cell(codeCol.Value).GetString().Trim();
                if (string.IsNullOrEmpty(courseCode)) continue;

                var startDate = startCol.HasValue ? ParseDateCell(row.Cell(startCol.Value)) : null;
                var endDate = endCol.HasValue ? ParseDateCell(row.Cell(endCol.Value)) : null;

                result.Add(new ClassImportRow(
                    row.RowNumber(),
                    className,
                    courseCode,
                    startDate,
                    endDate));
            }

            return result;
        }
    }

    public byte[] ExportClassTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Danh sách lớp học");
        sheet.ShowGridLines = true;

        // Title banner
        var titleRange = sheet.Range("A1:F1");
        titleRange.Merge();
        titleRange.Value = "MẪU IMPORT DANH SÁCH LỚP HỌC - PACE";
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 13;
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Fill.BackgroundColor = XLColor.FromArgb(185, 71, 32); // Brand Accent
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(1).Height = 32;

        // Subtitle note
        var noteRange = sheet.Range("A2:F2");
        noteRange.Merge();
        noteRange.Value = "Lưu ý: Giảng viên chỉ được tạo lớp học cho môn học mình phụ trách và trong học kỳ hiện tại.";
        noteRange.Style.Font.Italic = true;
        noteRange.Style.Font.FontSize = 10;
        noteRange.Style.Font.FontColor = XLColor.FromHtml("#4b5563");
        noteRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Row(2).Height = 20;

        // Headers
        var headers = new[] { "STT", "Tên lớp (*)", "Mã môn học (*)", "Ngày bắt đầu (dd/MM/yyyy)", "Ngày kết thúc (dd/MM/yyyy)", "Ghi chú" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(3, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f6");
            cell.Style.Alignment.Horizontal = (i == 0 || i == 2 || i == 3 || i == 4) ? XLAlignmentHorizontalValues.Center : XLAlignmentHorizontalValues.Left;
        }
        sheet.Row(3).Height = 24;

        // Sample Rows
        var sampleData = new[]
        {
            (1, "SE1703", "SWP391", "05/09/2026", "20/12/2026", "Lớp đồ án chuyên ngành"),
            (2, "IA1701", "PRN231", "05/09/2026", "20/12/2026", "Lớp lập trình đa nền tảng .NET")
        };

        for (int i = 0; i < sampleData.Length; i++)
        {
            var r = 4 + i;
            var (stt, cName, cCode, sDate, eDate, note) = sampleData[i];

            sheet.Cell(r, 1).Value = stt;
            sheet.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            sheet.Cell(r, 2).Value = cName;
            sheet.Cell(r, 2).Style.Font.Bold = true;

            sheet.Cell(r, 3).Value = cCode;
            sheet.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            sheet.Cell(r, 4).Value = sDate;
            sheet.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            sheet.Cell(r, 5).Value = eDate;
            sheet.Cell(r, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            sheet.Cell(r, 6).Value = note;

            sheet.Row(r).Height = 20;
        }

        // Borders
        var dataRange = sheet.Range(3, 1, 3 + sampleData.Length, 6);
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#d1d5db");
        dataRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#9ca3af");

        sheet.Columns(1, 6).AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static DateOnly? ParseDateCell(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;

        if (cell.DataType == XLDataType.DateTime)
        {
            var dt = cell.GetDateTime();
            return DateOnly.FromDateTime(dt);
        }

        var str = cell.GetString().Trim();
        if (string.IsNullOrEmpty(str)) return null;

        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy"];
        if (DateOnly.TryParseExact(str, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
            return d;

        if (DateOnly.TryParse(str, out var parsed))
            return parsed;

        return null;
    }
}

