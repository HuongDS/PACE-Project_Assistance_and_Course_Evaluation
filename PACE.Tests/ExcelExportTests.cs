using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PACE.Application.Dtos;
using PACE.Domain.Enums;
using PACE.Infrastructure.Excel;
using Xunit;

namespace PACE.Tests;

public class ExcelExportTests
{
    private static SurveySubmissionItemDto Row() => new(
        Guid.NewGuid(), Guid.NewGuid(), "student@gmail.com", "SE192251", "Repo A",
        "Chat", "Explain validation", "Checks input", "{\"comment\":\"Good\"}",
        "Understood", 0, DateTime.UtcNow, []);

    [Theory]
    [InlineData(ContextType.Raw, "Codebase")]
    [InlineData(ContextType.Graph, "Graph")]
    [InlineData(null, "Không xác định")]
    [InlineData((ContextType)99, "Không xác định")]
    public void ExcelExportsQuestionSourceAlongsideItsAnswer(ContextType? source, string label)
    {
        var row = Row() with { QuestionContextType = source };
        var summary = new SurveySummaryResponseDto(1, 1, 1, 4, "", "", [], [row]);
        using var stream = new MemoryStream(new ClosedXmlSurveyExcelService().ExportSurveyResults(summary));
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var sheet = workbook.Worksheet("Chi tiết khảo sát sinh viên");
        var headers = sheet.Row(1).CellsUsed().ToDictionary(c => c.GetString(), c => c.Address.ColumnNumber);
        Assert.Equal(label, sheet.Cell(2, headers["Nguồn sinh câu hỏi"]).GetString());
        Assert.Equal(row.StudentAnswerId.ToString(), sheet.Cell(2, headers["Student Answer ID"]).GetString());
    }

    [Fact]
    public void ExportReviewedSubmissionsEndpointHasAdminRole()
    {
        var method = typeof(PACE.API.Controllers.StudentSubmissionsController)
            .GetMethod(nameof(PACE.API.Controllers.StudentSubmissionsController.ExportReviewedSubmissions))!;
        var authorization = Assert.Single(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false));
        Assert.Equal(PACE.API.Controllers.Roles.Admin, ((Microsoft.AspNetCore.Authorization.AuthorizeAttribute)authorization).Roles);
    }
}

