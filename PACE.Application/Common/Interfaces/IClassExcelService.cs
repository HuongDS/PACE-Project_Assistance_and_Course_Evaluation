using PACE.Application.Dtos;

namespace PACE.Application.Common.Interfaces;

public record ClassStudentImportRow(int Row, string StudentCode, string? FullName = null, string? Email = null);

public interface IClassExcelService
{
    IReadOnlyList<ClassStudentImportRow> ReadStudentList(Stream excelStream);
    byte[] ExportStudentList(string className, IReadOnlyList<ClassStudentDto> students);
}

