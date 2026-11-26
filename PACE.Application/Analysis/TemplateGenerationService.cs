using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Flow;
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

        // Giai đoạn 1: Static Analysis & Dựng Đồ Thị Phụ Thuộc G = (V, E)
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

        // Giai đoạn 2 & 3: Graph Slicing, Trust Scoring & Selective Deobfuscation + Invariant Anchors
        var resolver = new CallResolver(parsed.Classes, parsed.Methods);
        var slicer = new EndpointSubgraphSlicer(resolver);
        var slices = slicer.SliceAll(endpoints, parsed.Methods);
        var slicesById = slices.ToDictionary(s => s.EndpointId);

        // Giai đoạn 4: Hybrid LLM Synthesis & Gom nhóm Nghiệp vụ
        var (source, businesses) = await SynthesizeAndGroupAsync(url, endpoints, slices, slicesById, request.UseAi, warnings, ct);
        return Result(url, source, string.Join(", ", scan.Stacks), BusinessTemplateLoader.Normalize(businesses), warnings);
    }

    public async Task<CompareTemplateResultDto> CompareAsync(CompareTemplateRequest request, CancellationToken ct = default)
    {
        if (RepoUrlHelper.FindProblem(request.RepoUrl) is { } problem) throw new BadRequestException(problem);
        var url = RepoUrlHelper.Normalize(request.RepoUrl);
        var baseWarnings = new List<string>();

        using var clone = await git.CloneAsync(url, ct);

        var scan = scanner.Scan(clone.WorkingPath, ct);
        baseWarnings.AddRange(scan.Warnings);
        if (scan.TotalFiles == 0)
            throw new UnprocessableException("Không tìm thấy mã nguồn backend C#, Java hoặc JavaScript/TypeScript trong repo.");

        var parsed = new ParseResult();
        foreach (var parser in parsers)
            parsed.Merge(parser.Parse(scan.FilesFor(parser.Kind), ct));
        baseWarnings.AddRange(parsed.Warnings);

        var endpoints = BusinessTemplateGenerator.ExtractEndpoints(parsed.Methods);
        if (endpoints.Count == 0)
            throw new UnprocessableException("Không tìm thấy controller/endpoint nào trong repo để tạo template.");

        var stacks = string.Join(", ", scan.Stacks);

        // 1. Chạy phương pháp CŨ (Legacy: chỉ dựa vào tên controller, http_method, function, path)
        var legacyWarnings = new List<string>(baseWarnings);
        var swLegacy = Stopwatch.StartNew();
        var (legacySource, legacyRaw) = await LegacyGroupAsync(url, endpoints, request.UseAi, legacyWarnings, ct);
        var legacyBusinesses = BusinessTemplateLoader.Normalize(legacyRaw);
        swLegacy.Stop();
        var legacyDto = Result(url, legacySource, stacks, legacyBusinesses, legacyWarnings);

        // 2. Chạy phương pháp MỚI (Hybrid: Graph Slicing + Trust Scoring + Selective Deobfuscation + Invariant Anchors + Hybrid LLM)
        var hybridWarnings = new List<string>(baseWarnings);
        var swHybrid = Stopwatch.StartNew();
        var resolver = new CallResolver(parsed.Classes, parsed.Methods);
        var slicer = new EndpointSubgraphSlicer(resolver);
        var slices = slicer.SliceAll(endpoints, parsed.Methods);
        var slicesById = slices.ToDictionary(s => s.EndpointId);

        var (hybridSource, hybridRaw) = await SynthesizeAndGroupAsync(url, endpoints, slices, slicesById, request.UseAi, hybridWarnings, ct);
        var hybridBusinesses = BusinessTemplateLoader.Normalize(hybridRaw);
        swHybrid.Stop();
        var hybridDto = Result(url, hybridSource, stacks, hybridBusinesses, hybridWarnings);

        // 3. Đối chiếu từng Endpoint giữa Cũ và Mới
        var legacyGroupByEndpoint = MapEndpointToBusiness(legacyBusinesses);
        var hybridGroupByEndpoint = MapEndpointToBusiness(hybridBusinesses);
        var hybridApiByEndpoint = MapEndpointToApi(hybridBusinesses);

        var endpointComparisons = new List<EndpointComparisonItemDto>(endpoints.Count);
        foreach (var ep in endpoints)
        {
            var key = EndpointKey(ep.Controller, ep.HttpMethod, ep.Function, ep.Path);
            var legacyBiz = legacyGroupByEndpoint.GetValueOrDefault(key, "(Chưa phân nhóm)");
            var hybridBiz = hybridGroupByEndpoint.GetValueOrDefault(key, "(Chưa phân nhóm)");
            var hybridApi = hybridApiByEndpoint.GetValueOrDefault(key);
            var slice = slicesById[ep.Id];

            endpointComparisons.Add(new EndpointComparisonItemDto(
                ep.Id,
                ep.Controller,
                ep.HttpMethod,
                ep.Function,
                ep.Path,
                legacyBiz,
                hybridBiz,
                !string.Equals(legacyBiz, hybridBiz, StringComparison.OrdinalIgnoreCase),
                hybridApi?.Summary ?? slice.FallbackSummary,
                hybridApi?.Behaviors?.Count ?? slice.FallbackBehaviors.Count,
                slice.EvidenceNodes.Count,
                slice.CodeAnchors));
        }

        var metrics = new TemplateComparisonMetricsDto(
            swLegacy.ElapsedMilliseconds,
            swHybrid.ElapsedMilliseconds,
            legacyDto.BusinessCount,
            hybridDto.BusinessCount,
            endpoints.Count,
            slices.Count(s => s.CodeAnchors.DbReads.Count > 0),
            slices.Count(s => s.CodeAnchors.DbWrites.Count > 0),
            slices.Count(s => s.CodeAnchors.ExternalSdks.Count > 0),
            endpointComparisons.Count(c => c.BusinessGroupChanged));

        return new CompareTemplateResultDto(url, stacks, metrics, legacyDto, hybridDto, endpointComparisons);
    }

    private async Task<(BusinessTemplateSource, List<BusinessTemplate>)> LegacyGroupAsync(
        string url,
        IReadOnlyList<TemplateEndpoint> endpoints,
        bool useAi,
        List<string> warnings,
        CancellationToken ct)
    {
        if (useAi && !ai.IsConfigured)
        {
            warnings.Add("Chưa cấu hình Gemini:ApiKey nên bản Legacy được gom theo controller.");
        }
        else if (useAi)
        {
            try
            {
                var groups = await ai.GroupAsync(endpoints, ct);
                return (BusinessTemplateSource.Ai, BusinessTemplateGenerator.ApplyGrouping(endpoints, groups, warnings));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Gemini Legacy gom nghiệp vụ cho {RepoUrl} thất bại", url);
                warnings.Add("Gemini Legacy không gom được nghiệp vụ nên đã gom theo controller.");
            }
        }

        return (BusinessTemplateSource.Controller, BusinessTemplateGenerator.GroupByController(endpoints));
    }

    private async Task<(BusinessTemplateSource, List<BusinessTemplate>)> SynthesizeAndGroupAsync(
        string url,
        IReadOnlyList<TemplateEndpoint> endpoints,
        IReadOnlyList<EndpointEvidenceSlice> slices,
        IReadOnlyDictionary<int, EndpointEvidenceSlice> slicesById,
        bool useAi,
        List<string> warnings,
        CancellationToken ct)
    {
        if (useAi && !ai.IsConfigured)
        {
            warnings.Add("Chưa cấu hình Gemini:ApiKey nên nghiệp vụ được gom theo controller kèm phân tích tĩnh Hybrid.");
        }
        else if (useAi)
        {
            try
            {
                var synthesis = await ai.SynthesizeAndGroupAsync(slices, ct);
                return (BusinessTemplateSource.Ai,
                    BusinessTemplateGenerator.ApplyGrouping(endpoints, synthesis.Groups, warnings, slicesById, synthesis.EndpointSpecs));
            }
            catch (NotSupportedException)
            {
                var groups = await ai.GroupAsync(endpoints, ct);
                return (BusinessTemplateSource.Ai,
                    BusinessTemplateGenerator.ApplyGrouping(endpoints, groups, warnings, slicesById));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Gemini Hybrid gom nghiệp vụ cho {RepoUrl} thất bại", url);
                warnings.Add("Gemini không gom được nghiệp vụ nên đã gom theo controller kèm phân tích tĩnh Hybrid.");
            }
        }

        return (BusinessTemplateSource.Controller, BusinessTemplateGenerator.GroupByController(endpoints, slicesById));
    }

    private static Dictionary<string, string> MapEndpointToBusiness(IEnumerable<BusinessTemplate> businesses)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in businesses)
        {
            foreach (var a in b.Apis)
            {
                var key = EndpointKey(a.Controller, a.ResolveHttpVerb() ?? string.Empty, a.ResolveFunctionName(), a.Path);
                map.TryAdd(key, b.BusinessName);
            }
        }
        return map;
    }

    private static Dictionary<string, ApiTemplate> MapEndpointToApi(IEnumerable<BusinessTemplate> businesses)
    {
        var map = new Dictionary<string, ApiTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in businesses)
        {
            foreach (var a in b.Apis)
            {
                var key = EndpointKey(a.Controller, a.ResolveHttpVerb() ?? string.Empty, a.ResolveFunctionName(), a.Path);
                map.TryAdd(key, a);
            }
        }
        return map;
    }

    private static string EndpointKey(string controller, string verb, string function, string? path)
        => $"{controller.Trim()}|{verb.Trim()}|{function.Trim()}|{(path ?? string.Empty).Trim()}";

    private static GeneratedTemplateDto Result(
        string url, BusinessTemplateSource source, string stacks, List<BusinessTemplate> businesses, List<string> warnings)
        => new(url, source, stacks, businesses.Count, businesses.Sum(b => b.Apis.Count), businesses, warnings);
}
