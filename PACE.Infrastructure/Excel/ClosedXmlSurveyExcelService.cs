using ClosedXML.Excel;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PACE.Infrastructure.Excel;

public class ClosedXmlSurveyExcelService : ISurveyExcelService
{
    public byte[] ExportSurveyResults(SurveySummaryResponseDto summary)
    {
        using var workbook = new XLWorkbook();

        // ================= SHEET 1: TỔNG QUAN KHẢO SÁT =================
        var ws1 = workbook.Worksheets.Add("Tổng quan thống kê");
        ws1.ShowGridLines = true;

        // Title
        var titleRange = ws1.Range("A1:K1");
        titleRange.Merge();
        titleRange.Value = "BÁO CÁO TỔNG HỢP KHẢO SÁT ĐỒNG THUẬN AI - HỆ THỐNG PACE";
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 14;
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Fill.BackgroundColor = XLColor.FromArgb(185, 71, 32); // Brand Accent
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws1.Row(1).Height = 35;

        // KPI Box
        int r = 3;
        ws1.Cell(r, 1).Value = "Tổng số bài đã nộp khớp bộ lọc:";
        ws1.Cell(r, 1).Style.Font.Bold = true;
        ws1.Cell(r, 2).Value = summary.TotalSubmissions;
        ws1.Cell(r, 2).Style.Font.Bold = true;

        ws1.Cell(r, 4).Value = "Điểm đồng thuận trung bình:";
        ws1.Cell(r, 4).Style.Font.Bold = true;
        ws1.Cell(r, 5).Value = summary.TotalRatings > 0 ? $"{summary.OverallAverageScore:F2} / 5.0 ⭐" : "—";
        ws1.Cell(r, 5).Style.Font.Bold = true;
        ws1.Cell(r, 5).Style.Font.FontColor = XLColor.FromHtml("#15803d");

        r++;
        ws1.Cell(r, 1).Value = "Tổng số câu trả lời khớp bộ lọc:";
        ws1.Cell(r, 1).Style.Font.Bold = true;
        ws1.Cell(r, 2).Value = summary.TotalEvaluatedAnswers;

        ws1.Cell(r, 4).Value = "Tiêu chí đồng thuận cao nhất:";
        ws1.Cell(r, 4).Style.Font.Bold = true;
        ws1.Cell(r, 5).Value = summary.HighestRatedCriterion;
        ws1.Cell(r, 5).Style.Font.FontColor = XLColor.FromHtml("#1d4ed8");

        r++;
        ws1.Cell(r, 1).Value = "Tổng số lượt đánh giá tiêu chí:";
        ws1.Cell(r, 1).Style.Font.Bold = true;
        ws1.Cell(r, 2).Value = summary.TotalRatings;

        ws1.Cell(r, 4).Value = "Tiêu chí cần cải thiện nhất:";
        ws1.Cell(r, 4).Style.Font.Bold = true;
        ws1.Cell(r, 5).Value = summary.LowestRatedCriterion;
        ws1.Cell(r, 5).Style.Font.FontColor = XLColor.FromHtml("#b91c1c");

        r++;
        ws1.Cell(r, 1).Value = "Thời gian xuất báo cáo (UTC):";
        ws1.Cell(r, 1).Style.Font.Bold = true;
        ws1.Cell(r, 2).Value = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss");

        // Criteria Table
        r = 8;
        ws1.Cell(r, 1).Value = "BẢNG THỐNG KÊ CHI TIẾT THEO TỪNG TIÊU CHÍ KHẢO SÁT";
        ws1.Cell(r, 1).Style.Font.Bold = true;
        ws1.Cell(r, 1).Style.Font.FontSize = 11;
        ws1.Cell(r, 1).Style.Font.FontColor = XLColor.FromHtml("#1e293b");

        r++;
        var headers1 = new[] { "STT", "Mã tiêu chí", "Tên tiêu chí", "Nội dung khẳng định (Statement)", "Điểm TB (1-5)", "Số lượt đánh giá", "1⭐ (Rất KĐY)", "2⭐ (KĐY)", "3⭐ (Bình thường)", "4⭐ (Đồng ý)", "5⭐ (Rất ĐY)", "Tỷ lệ đồng thuận (>=4⭐)" };
        for (int c = 0; c < headers1.Length; c++)
        {
            var cell = ws1.Cell(r, c + 1);
            cell.Value = headers1[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#334155");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws1.Row(r).Height = 25;

        int startRow1 = r + 1;
        int stt = 1;
        foreach (var stat in summary.CriteriaStats)
        {
            r++;
            ws1.Cell(r, 1).Value = stt++;
            ws1.Cell(r, 2).Value = stat.Code;
            ws1.Cell(r, 3).Value = stat.Name;
            ws1.Cell(r, 4).Value = stat.Statement;
            ws1.Cell(r, 5).Value = Math.Round(stat.AverageScore, 2);
            ws1.Cell(r, 6).Value = stat.TotalRatings;
            ws1.Cell(r, 7).Value = stat.Score1Count;
            ws1.Cell(r, 8).Value = stat.Score2Count;
            ws1.Cell(r, 9).Value = stat.Score3Count;
            ws1.Cell(r, 10).Value = stat.Score4Count;
            ws1.Cell(r, 11).Value = stat.Score5Count;

            double agreeRatio = stat.TotalRatings > 0 ? (double)(stat.Score4Count + stat.Score5Count) / stat.TotalRatings : 0;
            ws1.Cell(r, 12).Value = $"{agreeRatio:P1}";

            // Center small columns
            ws1.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell(r, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell(r, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell(r, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell(r, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell(r, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell(r, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell(r, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell(r, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell(r, 12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        if (r >= startRow1)
        {
            var dataTableRange = ws1.Range(startRow1 - 1, 1, r, headers1.Length);
            dataTableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataTableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataTableRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#cbd5e1");
            dataTableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#94a3b8");
        }

        ws1.Columns().AdjustToContents();
        ws1.Column(4).Width = 50; // Statement column
        ws1.Column(4).Style.Alignment.WrapText = true;

        // ================= SHEET 2: CHI TIẾT CÂU TRẢ LỜI & KHẢO SÁT =================
        var ws2 = workbook.Worksheets.Add("Chi tiết khảo sát sinh viên");
        ws2.ShowGridLines = true;

        // Dynamic criteria header list
        var criteriaList = summary.CriteriaStats.ToList();
        var headers2 = new List<string> {
            "STT", "Mã SV", "Email sinh viên", "Repository", "Nghiệp vụ", "Câu hỏi",
            "Câu trả lời SV", "Nhận xét của AI", "Ý kiến phản hồi SV (Student Reply)"
        };
        foreach (var c in criteriaList)
        {
            headers2.Add($"{c.Name} ({c.Code})");
        }
        headers2.Add("Điểm TB câu");
        headers2.Add("Thời gian nộp bài (UTC)");
        headers2.AddRange(new[] { "Loại feedback", "Repository ID", "Business ID", "Question ID", "Submission ID", "Student Answer ID" });

        int r2 = 1;
        for (int c = 0; c < headers2.Count; c++)
        {
            var cell = ws2.Cell(r2, c + 1);
            cell.Value = headers2[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(185, 71, 32);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws2.Row(r2).Height = 28;

        int rowStart2 = 2;
        int itemIndex = 1;
        foreach (var item in summary.Submissions)
        {
            int col = 1;
            ws2.Cell(rowStart2, col++).Value = itemIndex++;
            ws2.Cell(rowStart2, col++).Value = item.StudentCode ?? "—";
            ws2.Cell(rowStart2, col++).Value = item.StudentEmail;
            ws2.Cell(rowStart2, col++).Value = item.RepoName ?? "—";
            ws2.Cell(rowStart2, col++).Value = item.BusinessName;
            ws2.Cell(rowStart2, col++).Value = item.QuestionText;
            ws2.Cell(rowStart2, col++).Value = item.AnswerText;
            ws2.Cell(rowStart2, col++).Value = item.AiFeedback ?? "—";
            ws2.Cell(rowStart2, col++).Value = item.StudentReply ?? "—";

            // Scores per criterion
            foreach (var crit in criteriaList)
            {
                var match = item.Scores.FirstOrDefault(s => s.CriterionId == crit.CriterionId || s.Code == crit.Code);
                var scoreCell = ws2.Cell(rowStart2, col++);
                if (match != null)
                {
                    scoreCell.Value = match.Score;
                    scoreCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                else
                {
                    scoreCell.Value = "—";
                    scoreCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }

            var avgCell = ws2.Cell(rowStart2, col++);
            if (item.Scores.Count > 0) avgCell.Value = Math.Round(item.AverageScore, 2);
            else avgCell.Value = "—";
            avgCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            avgCell.Style.Font.Bold = true;

            var timeCell = ws2.Cell(rowStart2, col++);
            timeCell.Value = item.SubmittedAt.HasValue ? item.SubmittedAt.Value.ToString("dd/MM/yyyy HH:mm") : "—";
            timeCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws2.Cell(rowStart2, col++).Value = item.FeedbackFormat ?? "—";
            ws2.Cell(rowStart2, col++).Value = item.AnalysisRunId.ToString();
            ws2.Cell(rowStart2, col++).Value = item.BusinessId.ToString();
            ws2.Cell(rowStart2, col++).Value = item.QuestionId.ToString();
            ws2.Cell(rowStart2, col++).Value = item.SubmissionId.ToString();
            ws2.Cell(rowStart2, col++).Value = item.StudentAnswerId.ToString();
            rowStart2++;
        }

        if (rowStart2 > 2)
        {
            var dataTableRange2 = ws2.Range(1, 1, rowStart2 - 1, headers2.Count);
            dataTableRange2.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataTableRange2.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataTableRange2.Style.Border.InsideBorderColor = XLColor.FromHtml("#cbd5e1");
            dataTableRange2.Style.Border.OutsideBorderColor = XLColor.FromHtml("#94a3b8");
        }

        ws2.Columns().AdjustToContents();
        // Limit max width of long columns
        ws2.Column(6).Width = 35; // Question
        ws2.Column(7).Width = 40; // Answer
        ws2.Column(8).Width = 45; // AI Feedback
        ws2.Column(9).Width = 35; // Student Reply
        ws2.Column(6).Style.Alignment.WrapText = true;
        ws2.Column(7).Style.Alignment.WrapText = true;
        ws2.Column(8).Style.Alignment.WrapText = true;
        ws2.Column(9).Style.Alignment.WrapText = true;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public IReadOnlyList<UpsertRubricCriterionRequest> ReadCriteriaFromExcel(Stream stream)
    {
        // Check if stream might be a CSV or XLSX
        try
        {
            ExcelUploadGuard.Validate(stream);
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new BadRequestException("File Excel không có sheet nào.");
            var header = sheet.FirstRowUsed() ?? throw new BadRequestException("File Excel trống.");

            int? codeCol = null, nameCol = null, stmtCol = null, orderCol = null;
            foreach (var cell in header.CellsUsed())
            {
                var title = cell.GetString().Trim().ToLowerInvariant();
                if (codeCol is null && (title.Contains("code") || title.Contains("mã"))) codeCol = cell.Address.ColumnNumber;
                else if (nameCol is null && (title.Contains("name") || title.Contains("tên") || title.Contains("tiêu chí"))) nameCol = cell.Address.ColumnNumber;
                else if (stmtCol is null && (title.Contains("statement") || title.Contains("nội dung") || title.Contains("câu hỏi") || title.Contains("khẳng định"))) stmtCol = cell.Address.ColumnNumber;
                else if (title.Contains("order") || title.Contains("thứ tự")) orderCol = cell.Address.ColumnNumber;
                else if (orderCol is null && title.Contains("stt")) orderCol = cell.Address.ColumnNumber;
            }

            if (codeCol is null || nameCol is null || stmtCol is null)
                throw new BadRequestException("Dòng tiêu đề cần có ít nhất các cột: Mã (Code), Tên (Name), Nội dung (Statement).");

            var result = new List<UpsertRubricCriterionRequest>();
            int fallbackOrder = 1;
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > header.RowNumber()))
            {
                var code = row.Cell(codeCol.Value).GetString().Trim().ToUpperInvariant();
                var name = row.Cell(nameCol.Value).GetString().Trim();
                var statement = row.Cell(stmtCol.Value).GetString().Trim();
                if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name)) continue;

                int order = fallbackOrder;
                if (orderCol.HasValue && int.TryParse(row.Cell(orderCol.Value).GetString().Trim(), out var parsedOrder) && parsedOrder > 0)
                {
                    order = parsedOrder;
                }
                fallbackOrder = Math.Max(fallbackOrder, order) + 1;

                result.Add(new UpsertRubricCriterionRequest
                {
                    Code = code,
                    Name = name,
                    Statement = statement,
                    DisplayOrder = order
                });
            }
            return result;
        }
        catch (Exception ex) when (ex is not BadRequestException)
        {
            // Try parsing as CSV if not a valid xlsx
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (!string.IsNullOrWhiteSpace(line)) lines.Add(line);
            }

            if (lines.Count < 2) throw new BadRequestException("File không có dữ liệu hợp lệ (cần định dạng .xlsx hoặc .csv có dòng tiêu đề).");

            var delimiter = lines[0].Contains(';') ? ';' : lines[0].Contains('\t') ? '\t' : ',';
            var headerCells = lines[0].Split(delimiter).Select(c => c.Trim().Trim('"').ToLowerInvariant()).ToArray();

            int? codeCol = null, nameCol = null, stmtCol = null, orderCol = null;
            for (int i = 0; i < headerCells.Length; i++)
            {
                var title = headerCells[i];
                if (codeCol is null && (title.Contains("code") || title.Contains("mã"))) codeCol = i;
                else if (nameCol is null && (title.Contains("name") || title.Contains("tên") || title.Contains("tiêu chí"))) nameCol = i;
                else if (stmtCol is null && (title.Contains("statement") || title.Contains("nội dung") || title.Contains("câu hỏi") || title.Contains("khẳng định"))) stmtCol = i;
                else if (title.Contains("order") || title.Contains("thứ tự")) orderCol = i;
                else if (orderCol is null && title.Contains("stt")) orderCol = i;
            }

            if (codeCol is null || nameCol is null || stmtCol is null)
                throw new BadRequestException("File CSV cần có tiêu đề các cột: Mã (Code), Tên (Name), Nội dung (Statement).");

            var result = new List<UpsertRubricCriterionRequest>();
            int fallbackOrder = 1;
            for (int i = 1; i < lines.Count; i++)
            {
                var cols = lines[i].Split(delimiter).Select(c => c.Trim().Trim('"')).ToArray();
                if (cols.Length <= Math.Max(codeCol.Value, Math.Max(nameCol.Value, stmtCol.Value))) continue;

                var code = cols[codeCol.Value].Trim().ToUpperInvariant();
                var name = cols[nameCol.Value].Trim();
                var statement = cols[stmtCol.Value].Trim();
                if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(name)) continue;

                int order = fallbackOrder;
                if (orderCol.HasValue && orderCol.Value < cols.Length && int.TryParse(cols[orderCol.Value].Trim(), out var parsedOrder) && parsedOrder > 0)
                {
                    order = parsedOrder;
                }
                fallbackOrder = Math.Max(fallbackOrder, order) + 1;

                result.Add(new UpsertRubricCriterionRequest
                {
                    Code = code,
                    Name = name,
                    Statement = statement,
                    DisplayOrder = order
                });
            }

            return result;
        }
    }

    public byte[] GenerateCriteriaTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Tiêu chí khảo sát");
        ws.ShowGridLines = true;

        var headers = new[] { "Mã tiêu chí (Code)", "Tên tiêu chí (Name)", "Nội dung câu hỏi khảo sát (Statement)", "Thứ tự hiển thị (DisplayOrder)" };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(185, 71, 32);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(1).Height = 26;

        var samples = new[]
        {
            new { Code = "ACCURACY", Name = "Độ chính xác kỹ thuật", Statement = "Nhận xét đúng về mặt kỹ thuật so với code thực tế trong repo của nhóm tôi.", Order = 1 },
            new { Code = "FAIRNESS", Name = "Tính công bằng", Statement = "Điểm số và nhận xét phản ánh công bằng chất lượng câu trả lời của tôi.", Order = 2 },
            new { Code = "SPECIFICITY", Name = "Độ cụ thể", Statement = "Nhận xét chỉ rõ phần nào trong câu trả lời của tôi đúng, phần nào sai hoặc còn thiếu.", Order = 3 },
            new { Code = "ACTIONABILITY", Name = "Có thể hành động", Statement = "Sau khi đọc, tôi biết cụ thể cần làm gì để cải thiện câu trả lời.", Order = 4 },
            new { Code = "USEFULNESS", Name = "Tính hữu ích", Statement = "Nhận xét này giúp tôi hiểu rõ hơn luồng xử lý trong project của mình.", Order = 5 }
        };

        for (int i = 0; i < samples.Length; i++)
        {
            int row = i + 2;
            ws.Cell(row, 1).Value = samples[i].Code;
            ws.Cell(row, 2).Value = samples[i].Name;
            ws.Cell(row, 3).Value = samples[i].Statement;
            ws.Cell(row, 4).Value = samples[i].Order;

            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var tableRange = ws.Range(1, 1, samples.Length + 1, 4);
        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#cbd5e1");
        tableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#94a3b8");

        ws.Columns().AdjustToContents();
        ws.Column(3).Width = 55;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportCriteria(IReadOnlyList<RubricCriterionDto> criteria)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Tiêu chí khảo sát");
        ws.ShowGridLines = true;

        var headers = new[] { "STT", "Mã tiêu chí (Code)", "Tên tiêu chí (Name)", "Nội dung câu hỏi khảo sát (Statement)", "Thứ tự hiển thị (DisplayOrder)" };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(185, 71, 32);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(1).Height = 26;

        int row = 2;
        int stt = 1;
        foreach (var c in criteria.OrderBy(x => x.DisplayOrder))
        {
            ws.Cell(row, 1).Value = stt++;
            ws.Cell(row, 2).Value = c.Code;
            ws.Cell(row, 3).Value = c.Name;
            ws.Cell(row, 4).Value = c.Statement;
            ws.Cell(row, 5).Value = c.DisplayOrder;

            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row++;
        }

        if (criteria.Count > 0)
        {
            var tableRange = ws.Range(1, 1, row - 1, headers.Length);
            tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            tableRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#cbd5e1");
            tableRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#94a3b8");
        }

        ws.Columns().AdjustToContents();
        ws.Column(1).Width = 8;
        ws.Column(2).Width = 20;
        ws.Column(3).Width = 30;
        ws.Column(4).Width = 60;
        ws.Column(4).Style.Alignment.WrapText = true;
        ws.Column(5).Width = 20;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
