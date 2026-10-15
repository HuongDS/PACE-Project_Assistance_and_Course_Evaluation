using System.IO.Compression;
using System.Text;
using PACE.Application.Common;
using PACE.Application.Dtos;

namespace PACE.Application.Analysis.Template;

public static class TemplateArchiveBuilder
{
    public const string ReportFileName = "_report.txt";

    /// <summary>Zip gồm <c>owner__repo/template_business.json</c> cho mỗi repo thành công và <c>_report.txt</c> liệt kê kết quả từng dòng.</summary>
    public static byte[] Build(BatchTemplateResultDto result)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var report = new StringBuilder();
            report.AppendLine($"Tổng {result.Total} dòng: {result.Succeeded} thành công, {result.Failed} lỗi, {result.Skipped} bỏ qua.");

            foreach (var item in result.Items)
            {
                if (item.Status == BatchItemStatus.Succeeded && item.Template is { } template)
                {
                    var folder = UniqueFolder(item.RepoUrl, usedFolders);
                    Write(zip, $"{folder}/{BusinessTemplateLoader.FileName}", BusinessTemplateGenerator.Serialize(template.Businesses));
                    report.AppendLine($"Dòng {item.Row}: OK  {item.RepoUrl} -> {folder}/ ({template.BusinessCount} nghiệp vụ, {template.ApiCount} API, nguồn {template.Source})");
                    foreach (var warning in template.Warnings) report.AppendLine($"    cảnh báo: {warning}");
                }
                else
                {
                    report.AppendLine($"Dòng {item.Row}: {item.Status.ToString().ToUpperInvariant()}  {item.RepoUrl}  {item.Error}");
                }
            }

            Write(zip, ReportFileName, report.ToString());
        }

        return stream.ToArray();
    }

    internal static string UniqueFolder(string repoUrl, ISet<string> used)
    {
        var baseName = new string(RepoUrlHelper.DeriveName(repoUrl).Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray());
        var name = baseName;
        for (var n = 2; !used.Add(name); n++) name = $"{baseName}_{n}";
        return name;
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
