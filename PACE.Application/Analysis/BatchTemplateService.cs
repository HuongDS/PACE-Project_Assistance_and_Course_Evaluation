using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PACE.Application.Common;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.Application.Analysis;

public sealed class BatchTemplateService(
    IRepoLinkReader reader,
    IServiceScopeFactory scopes,
    ILogger<BatchTemplateService> logger) : IBatchTemplateService
{
    public const int MaxRepos = 50;
    private const int Parallelism = 3;
    private const int MaxErrorLength = 500;

    public async Task<BatchTemplateResultDto> GenerateAsync(Stream excel, bool useAi, bool regenerate, CancellationToken ct = default)
    {
        var rows = reader.Read(excel);
        if (rows.Count == 0) throw new BadRequestException("File Excel không có link repo nào.");

        var items = new BatchTemplateItemDto?[rows.Count];
        var work = new List<(int Index, int Row, string Url)>();
        var firstRowOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!RepoUrlHelper.IsSupportedUrl(row.RepoUrl))
            {
                items[i] = new BatchTemplateItemDto(row.Row, row.RepoUrl, BatchItemStatus.Failed, "Link repo không hợp lệ.", null);
                continue;
            }

            var url = RepoUrlHelper.Normalize(row.RepoUrl);
            if (firstRowOf.TryGetValue(url, out var firstRow))
            {
                items[i] = new BatchTemplateItemDto(row.Row, url, BatchItemStatus.Skipped, $"Trùng với dòng {firstRow}.", null);
                continue;
            }

            firstRowOf[url] = row.Row;
            work.Add((i, row.Row, url));
        }

        if (work.Count > MaxRepos)
            throw new BadRequestException($"Mỗi lần tối đa {MaxRepos} repo, file có {work.Count} repo. Hãy chia nhỏ file.");

        await Parallel.ForEachAsync(work, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct },
            async (w, token) => items[w.Index] = await GenerateOneAsync(w.Row, w.Url, useAi, regenerate, token));

        var result = items.Select(i => i!).ToList();
        return new BatchTemplateResultDto(result.Count,
            result.Count(i => i.Status == BatchItemStatus.Succeeded),
            result.Count(i => i.Status == BatchItemStatus.Failed),
            result.Count(i => i.Status == BatchItemStatus.Skipped),
            result);
    }

    private async Task<BatchTemplateItemDto> GenerateOneAsync(int row, string url, bool useAi, bool regenerate, CancellationToken ct)
    {
        try
        {
            // Mỗi repo một scope riêng vì DbContext không an toàn khi dùng song song.
            using var scope = scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITemplateGenerationService>();
            var template = await service.GenerateAsync(new GenerateTemplateRequest { RepoUrl = url, UseAi = useAi, Regenerate = regenerate }, ct);
            return new BatchTemplateItemDto(row, url, BatchItemStatus.Succeeded, null, template);
        }
        catch (AppException ex)
        {
            return new BatchTemplateItemDto(row, url, BatchItemStatus.Failed, Summarize(ex.Message), null);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Tạo template hàng loạt thất bại ở dòng {Row} ({RepoUrl})", row, url);
            return new BatchTemplateItemDto(row, url, BatchItemStatus.Failed, "Lỗi không xác định khi xử lý repo này.", null);
        }
    }

    // Gộp thành một dòng và bỏ dòng "Cloning into '<thư mục tạm>'" để báo cáo gọn, không lộ đường dẫn của server.
    public static string Summarize(string message)
    {
        var lines = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.StartsWith("[git] Clone thất bại: Cloning into", StringComparison.Ordinal) ? "[git] Clone thất bại." : l)
            .Where(l => !l.StartsWith("Cloning into", StringComparison.Ordinal));
        var oneLine = string.Join(' ', lines);
        return oneLine.Length <= MaxErrorLength ? oneLine : oneLine[..MaxErrorLength] + "...";
    }
}
