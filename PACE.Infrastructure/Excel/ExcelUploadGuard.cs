using System.IO.Compression;
using PACE.Application.Common.Exceptions;
namespace PACE.Infrastructure.Excel;
internal static class ExcelUploadGuard
{
    public static void Validate(Stream stream)
    {
        if (!stream.CanSeek || stream.Length is <= 0 or > 10 * 1024 * 1024) throw new BadRequestException("Workbook must be nonempty, seekable and at most 10 MB.");
        var position = stream.Position;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > 1000 || archive.Entries.Sum(e => e.Length) > 50L * 1024 * 1024)
                throw new BadRequestException("Workbook exceeds 1000 ZIP entries or 50 MB expanded size.");
            if (archive.GetEntry("[Content_Types].xml") is null) throw new BadRequestException("Invalid .xlsx package.");
        }
        catch (InvalidDataException) { throw new BadRequestException("Invalid .xlsx archive."); }
        finally { stream.Position = position; }
    }
}
