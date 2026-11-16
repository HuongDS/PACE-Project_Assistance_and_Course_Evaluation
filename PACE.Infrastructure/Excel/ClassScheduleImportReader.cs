using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;
using PACE.Application.Common.Exceptions;

namespace PACE.Infrastructure.Excel;

public record ClassScheduleImportRow(int Line, string Kind, string Title, string DueAt, string Description);

public static class ClassScheduleImportReader
{
    public const int MaxRows = 500;
    private static readonly string[] Headers = ["kind", "title", "dueAt", "description"];

    public static IReadOnlyList<ClassScheduleImportRow> Read(Stream stream, string extension, CancellationToken ct)
    {
        try
        {
            return extension switch
            {
                ".csv" => ReadCsv(stream, ct),
                ".xlsx" => ReadExcel(stream, ct),
                _ => throw new BadRequestException("Upload an .xlsx or .csv file.")
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or MalformedLineException
            or FormatException or ArgumentException or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            throw new BadRequestException("The milestone import file is invalid.");
        }
    }

    private static List<ClassScheduleImportRow> ReadCsv(Stream stream, CancellationToken ct)
    {
        using var parser = new TextFieldParser(stream);
        parser.SetDelimiters(",");
        ValidateHeaders(parser.ReadFields() ?? []);
        var rows = new List<ClassScheduleImportRow>();
        while (!parser.EndOfData)
        {
            ct.ThrowIfCancellationRequested();
            var line = (int)parser.LineNumber;
            var fields = parser.ReadFields() ?? [];
            if (fields.Length is < 3 or > 4)
                throw new BadRequestException($"Row {line}: expected kind, title, dueAt and optional description.");
            AddRow(rows, new(line, fields[0], fields[1], fields[2], fields.ElementAtOrDefault(3) ?? ""));
        }
        return rows;
    }

    private static List<ClassScheduleImportRow> ReadExcel(Stream stream, CancellationToken ct)
    {
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            const long maxExpandedBytes = 32 * 1024 * 1024;
            if (archive.Entries.Count > 256 ||
                archive.Entries.Any(entry => entry.Length > maxExpandedBytes) ||
                archive.Entries.Sum(entry => entry.Length) > maxExpandedBytes)
                throw new BadRequestException("The workbook contains too much data.");
        }
        stream.Position = 0;

        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new BadRequestException("The workbook has no sheets.");
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow > MaxRows + 1)
            throw new BadRequestException($"Import at most {MaxRows} data rows.");
        ValidateHeaders(Enumerable.Range(1, 4).Select(column => sheet.Cell(1, column).GetString()).ToArray());

        var rows = new List<ClassScheduleImportRow>();
        for (var line = 2; line <= lastRow; line++)
        {
            ct.ThrowIfCancellationRequested();
            var cells = Enumerable.Range(1, 4).Select(column => sheet.Cell(line, column)).ToArray();
            if (cells.All(cell => cell.IsEmpty()))
                continue;
            if (cells.Any(cell => cell.HasFormula))
                throw new BadRequestException($"Row {line}: use values instead of formulas.");
            var dueAt = cells[2].DataType == XLDataType.DateTime
                ? DateTime.SpecifyKind(cells[2].GetDateTime(), DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture)
                : cells[2].GetString();
            AddRow(rows, new(line, cells[0].GetString(), cells[1].GetString(), dueAt, cells[3].GetString()));
        }
        return rows;
    }

    private static void ValidateHeaders(string[] fields)
    {
        if (fields.Length is < 3 or > 4 || !fields.Select(field => field.Trim())
            .SequenceEqual(Headers.Take(fields.Length), StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("Expected headers: kind,title,dueAt,description.");
    }

    private static void AddRow(List<ClassScheduleImportRow> rows, ClassScheduleImportRow row)
    {
        if (rows.Count == MaxRows)
            throw new BadRequestException($"Import at most {MaxRows} data rows.");
        rows.Add(row);
    }
}
