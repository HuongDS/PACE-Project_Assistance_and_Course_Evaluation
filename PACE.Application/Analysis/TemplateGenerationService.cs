using Microsoft.Extensions.Logging;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Application.Common;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.Application.Analysis;

public sealed class TemplateGenerationService(
    IGitCloneService git,
    IRepositoryScanner scanner,
    IEnumerable<ILanguageParser> parsers,
    IBusinessGroupingAi ai,
    ILogger<TemplateGenerationService> logger) : ITemplateGenerationService
{
    public async Task<GeneratedTemplateDto> GenerateAsync(GenerateTemplateRequest request, CancellationToken ct = default)
    {
        if (!RepoUrlHelper.IsSupportedUrl(request.RepoUrl)) throw new BadRequestException("Link repo không hợp lệ.");
        var url = RepoUrlHelper.Normalize(request.RepoUrl);
        var warnings = new List<string>();

        using var clone = await git.CloneAsync(url, ct);

        if (!request.Regenerate)
        {
            var existing = await scanner.ReadBusinessTemplateAsync([clone.WorkingPath, clone.RootPath], ct);
            if (existing is not null)
            {
                try
                {
                    var templates = BusinessTemplateLoader.Parse(existing);
                    return Result(url, TemplateSource.Repository, string.Empty, templates, warnings);
                }
                catch (BadRequestException ex)
                {
                    warnings.Add($"File {BusinessTemplateLoader.FileName} trong repo không dùng được ({ex.Message}), đã tạo mới.");
                }
            }
        }

        var scan = scanner.Scan(clone.WorkingPath, ct);
        warnings.AddRange(scan.Warnings);
        if (scan.TotalFiles == 0)
            throw new UnprocessableException("Không tìm thấy mã nguồn backend C#, Java hoặc JavaScript/TypeScript trong repo.");

        var parsed = new ParseResult();
        foreach (var parser in parsers)
            parsed.Merge(parser.Parse(scan.FilesFor(parser.Kind), ct));
        warnings.AddRange(parsed.Warnings);

        var endpoints = BusinessTemplateGenerator.ExtractEndpoints(parsed.Methods);
        if (endpoints.Count == 0)
            throw new UnprocessableException("Không tìm thấy controller/endpoint nào trong repo để tạo template.");

        var (source, businesses) = await GroupAsync(url, endpoints, request.UseAi, warnings, ct);
        return Result(url, source, string.Join(", ", scan.Stacks), BusinessTemplateLoader.Normalize(businesses), warnings);
    }

    private async Task<(TemplateSource, List<BusinessTemplate>)> GroupAsync(
        string url, IReadOnlyList<TemplateEndpoint> endpoints, bool useAi, List<string> warnings, CancellationToken ct)
    {
        if (useAi && !ai.IsConfigured)
            warnings.Add("Chưa cấu hình Gemini:ApiKey nên nghiệp vụ được gom theo controller.");
        else if (useAi)
        {
            try
            {
                var groups = await ai.GroupAsync(endpoints, ct);
                return (TemplateSource.Ai, BusinessTemplateGenerator.ApplyGrouping(endpoints, groups, warnings));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Gemini gom nghiệp vụ cho {RepoUrl} thất bại", url);
                warnings.Add("Gemini không gom được nghiệp vụ nên đã gom theo controller.");
            }
        }

        return (TemplateSource.Controller, BusinessTemplateGenerator.GroupByController(endpoints));
    }

    private static GeneratedTemplateDto Result(
        string url, TemplateSource source, string stacks, List<BusinessTemplate> businesses, List<string> warnings)
        => new(url, source, stacks, businesses.Count, businesses.Sum(b => b.Apis.Count), businesses, warnings);
}
