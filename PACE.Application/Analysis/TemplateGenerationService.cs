using Microsoft.Extensions.Logging;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Application.Common;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Enums;

namespace PACE.Application.Analysis;

public sealed class TemplateGenerationService(
    IUnitOfWork uow,
    IGitCloneService git,
    IRepositoryScanner scanner,
    IEnumerable<ILanguageParser> parsers,
    IBusinessGroupingAi ai,
    ILogger<TemplateGenerationService> logger) : ITemplateGenerationService
{
    public async Task<GeneratedTemplateDto> GenerateAsync(GenerateTemplateRequest request, CancellationToken ct = default)
    {
        if (RepoUrlHelper.FindProblem(request.RepoUrl) is { } problem) throw new BadRequestException(problem);
        var url = RepoUrlHelper.Normalize(request.RepoUrl);
        var warnings = new List<string>();

        if (!request.Regenerate)
        {
            var run = await uow.AnalysisRuns.GetByRepoUrlAsync(url, ct);
            if (run is { TemplateSource: BusinessTemplateSource.Confirmed, BusinessTemplateJson: not null })
                return Result(url, BusinessTemplateSource.Confirmed, string.Empty, BusinessTemplateLoader.Parse(run.BusinessTemplateJson), warnings);
        }

        using var clone = await git.CloneAsync(url, ct);

        if (!request.Regenerate)
        {
            var existing = await scanner.ReadBusinessTemplateAsync([clone.WorkingPath, clone.RootPath], ct);
            if (existing is not null)
            {
                try
                {
                    var templates = BusinessTemplateLoader.Parse(existing);
                    return Result(url, BusinessTemplateSource.Repository, string.Empty, templates, warnings);
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
            throw new UnprocessableException("Không tìm thấy mã nguồn được hỗ trợ trong repo.");

        var parsed = new ParseResult();
        foreach (var parser in parsers)
            parsed.Merge(parser.Parse(scan.FilesFor(parser.Kind), ct));
        warnings.AddRange(parsed.Warnings);

        var endpoints = BusinessTemplateGenerator.ExtractEndpoints(parsed.Methods);
        if (endpoints.Count == 0 && scan.FrontendFiles.Count > 0)
            return Result(url, BusinessTemplateSource.Controller, string.Join(", ", scan.Stacks),
                BusinessTemplateGenerator.GroupFrontend(scan.FrontendEntries()), warnings);

        if (endpoints.Count == 0)
            throw new UnprocessableException("Không tìm thấy controller/endpoint nào trong repo để tạo template.");

        var (source, businesses) = await GroupAsync(url, endpoints, request.UseAi, warnings, ct);
        return Result(url, source, string.Join(", ", scan.Stacks), BusinessTemplateLoader.Normalize(businesses.Concat(BusinessTemplateGenerator.GroupFrontend(scan.FrontendEntries()))), warnings);
    }

    private async Task<(BusinessTemplateSource, List<BusinessTemplate>)> GroupAsync(
        string url, IReadOnlyList<TemplateEndpoint> endpoints, bool useAi, List<string> warnings, CancellationToken ct)
    {
        if (useAi && !ai.IsConfigured)
            warnings.Add("Chưa cấu hình Gemini:ApiKey nên nghiệp vụ được gom theo controller.");
        else if (useAi)
        {
            try
            {
                var groups = await ai.GroupAsync(endpoints, ct);
                return (BusinessTemplateSource.Ai, BusinessTemplateGenerator.ApplyGrouping(endpoints, groups, warnings));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Gemini gom nghiệp vụ cho {RepoUrl} thất bại", url);
                warnings.Add("Gemini không gom được nghiệp vụ nên đã gom theo controller.");
            }
        }

        return (BusinessTemplateSource.Controller, BusinessTemplateGenerator.GroupByController(endpoints));
    }

    private static GeneratedTemplateDto Result(
        string url, BusinessTemplateSource source, string stacks, List<BusinessTemplate> businesses, List<string> warnings)
        => new(url, source, stacks, businesses.Count, businesses.Sum(b => b.Apis.Count), businesses, warnings);
}
