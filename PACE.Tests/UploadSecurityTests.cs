using System.IO.Compression;
using PACE.Application.Common.Exceptions;
using PACE.Infrastructure.Excel;
using Xunit;
namespace PACE.Tests;
public class UploadSecurityTests
{
    [Fact]
    public void CorruptXlsxIsRejectedBeforeParsing()
    {
        using var stream = new MemoryStream([1, 2, 3, 4]);
        Assert.Throws<BadRequestException>(() => new ClosedXmlUserImportReader().Read(stream));
    }
    [Fact]
    public void ExpandedWorkbookLimitRejectsZipBomb()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using var target = archive.CreateEntry("oversized.xml", CompressionLevel.Fastest).Open();
            var bytes = new byte[1024 * 1024]; for (int i = 0; i < 51; i++) target.Write(bytes);
        }
        stream.Position = 0;
        var error = Assert.Throws<BadRequestException>(() => new ClosedXmlRepoLinkReader().Read(stream));
        Assert.Contains("expanded", error.Message);
    }
}
