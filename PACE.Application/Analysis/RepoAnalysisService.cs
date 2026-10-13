using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Common;
using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Analysis;

public sealed class RepoAnalysisService(
    IUnitOfWork uow,
    IGitCloneService git,
    IRepositoryScanner scanner,
    IEnumerable<ILanguageParser> parsers,
    IOptions<AnalysisOptions> options,
    ILogger<RepoAnalysisService> logger) : IRepoAnalysisService
{
    private sealed record AnalysisModel(
        List<MethodSource> Methods,
        List<Business> Businesses,
        List<BusinessMethod> BusinessMethods,
        List<BusinessFlow> Flows,
        List<DataFlowEdge> Edges);

    public async Task<AnalyzeResultDto> CreateAndAnalyzeAsync(CreateAnalysisRunRequest request, CancellationToken ct = default)
    {
        var url = RepoUrlHelper.Normalize(request.RepoUrl);
        var run = await uow.AnalysisRuns.GetByRepoUrlAsync(url, ct);
        var created = run is null;

        if (run is null)
        {
            run = new AnalysisRun
            {
                Id = Guid.NewGuid(),
                RepoUrl = url,
                RepoName = string.IsNullOrWhiteSpace(request.RepoName) ? RepoUrlHelper.DeriveName(url) : request.RepoName.Trim()
            };
            await uow.AnalysisRuns.AddAsync(run, ct);
            await uow.SaveChangesAsync(ct);
        }

        try
        {
            return await AnalyzeAsync(run.Id, ct);
        }
        catch
        {
            if (created) await RemoveRunAsync(run.Id);
            throw;
        }
    }

    private async Task RemoveRunAsync(Guid runId)
    {
        try
        {
            uow.ClearChangeTracker();
            var run = await uow.AnalysisRuns.GetByIdAsync(runId, CancellationToken.None);
            if (run is null) return;
            uow.AnalysisRuns.Remove(run);
            await uow.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không xoá được repo {RunId} sau khi phân tích thất bại", runId);
        }
    }

    private async Task<AnalyzeResultDto> AnalyzeAsync(Guid analysisRunId, CancellationToken ct)
    {
        var run = await uow.AnalysisRuns.GetByIdAsync(analysisRunId, ct) ?? throw new NotFoundException("repo", analysisRunId);
        if (string.IsNullOrWhiteSpace(run.RepoUrl)) throw new BadRequestException("Repo chưa có link GitHub.");
        if (await uow.AnalysisRuns.HasQuestionsAsync(analysisRunId, ct))
            throw new ConflictException("Repo đã có câu hỏi. Phân tích lại sẽ xoá nghiệp vụ cũ kéo theo câu hỏi và câu trả lời (cascade); gọi với force=true nếu chắc chắn.");

        if (!AnalysisRunLock.TryAcquire(analysisRunId))
            throw new ConflictException("Repo này đang được phân tích, vui lòng đợi lần chạy trước xong.");

        var warnings = new List<string>();
        try
        {
            using var clone = await git.CloneAsync(run.RepoUrl, ct);

            var templateJson = await scanner.ReadBusinessTemplateAsync([clone.WorkingPath, clone.RootPath], ct)
                               ?? throw new BadRequestException($"Repo không có file {BusinessTemplateLoader.FileName}.");
            var templates = BusinessTemplateLoader.Parse(templateJson);

            var scan = scanner.Scan(clone.WorkingPath, ct);
            warnings.AddRange(scan.Warnings);
            if (scan.TotalFiles == 0)
                throw new UnprocessableException("Không tìm thấy mã nguồn backend C#, Java hoặc JavaScript/TypeScript trong repo.");

            var parsed = new ParseResult();
            foreach (var parser in parsers)
                parsed.Merge(parser.Parse(scan.FilesFor(parser.Kind), ct));
            warnings.AddRange(parsed.Warnings);

            if (parsed.Methods.Count == 0)
                throw new UnprocessableException("Parse xong nhưng không tìm thấy hàm nào trong mã nguồn.");

            var model = BuildModel(run.Id, templates, parsed, warnings);

            await uow.ExecuteInTransactionAsync(async token =>
            {
                await uow.Businesses.DeleteByRepoAsync(analysisRunId, token);
                await uow.MethodSources.DeleteByRepoAsync(analysisRunId, token);

                await uow.MethodSources.AddRangeAsync(model.Methods, token);
                await uow.Businesses.AddRangeAsync(model.Businesses, token);
                await uow.BusinessMethods.AddRangeAsync(model.BusinessMethods, token);
                await uow.BusinessFlows.AddRangeAsync(model.Flows, token);
                await uow.DataFlowEdges.AddRangeAsync(model.Edges, token);
            }, ct);

            logger.LogInformation("Phân tích {RepoUrl}: {Methods} hàm, {Businesses} nghiệp vụ, {Flows} luồng",
                run.RepoUrl, model.Methods.Count, model.Businesses.Count, model.Flows.Count);

            return new AnalyzeResultDto(run.Id, string.Join(", ", scan.Stacks), model.Methods.Count, model.Businesses.Count,
                model.Flows.Count, model.Edges.Count, warnings);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Phân tích repo {RunId} thất bại", analysisRunId);
            throw;
        }
        finally
        {
            AnalysisRunLock.Release(analysisRunId);
        }
    }

    private AnalysisModel BuildModel(Guid runId, List<BusinessTemplate> templates, ParseResult parsed, List<string> warnings)
    {
        var settings = options.Value;
        var resolver = new CallResolver(parsed.Classes, parsed.Methods);
        var matcher = new EntryPointMatcher(parsed.Methods);
        var builder = new FlowBuilder(resolver, settings.MaxFlowDepth, settings.MaxStepsPerFlow);

        var methods = parsed.Methods.Select(m => new MethodSource
        {
            Id = m.Id,
            AnalysisRunId = runId,
            FilePath = Truncate(m.FilePath, 500),
            ClassName = Truncate(m.ClassName, 255)!,
            MethodName = Truncate(m.MethodName, 255)!,
            Parameters = m.Parameters.Count == 0 ? null : string.Join(", ", m.Parameters.Select(p => p.ToString())),
            SourceCode = m.SourceCode,
            Language = m.Language,
            NodeType = m.IsDecision ? NodeType.DecisionGateway : NodeType.Activity
        }).ToList();

        var businesses = new List<Business>();
        var businessMethods = new List<BusinessMethod>();
        var flows = new List<BusinessFlow>();
        var edges = new List<DataFlowEdge>();

        foreach (var template in templates)
        {
            var business = new Business
            {
                Id = Guid.NewGuid(),
                AnalysisRunId = runId,
                BusinessName = template.BusinessName
            };
            businesses.Add(business);

            var methodIds = new HashSet<Guid>();
            var entries = new HashSet<Guid>();

            foreach (var api in template.Apis)
            {
                var entry = matcher.Match(api, out var note);
                if (note is not null) warnings.Add($"[{template.BusinessName}] {note}");
                if (entry is null)
                {
                    warnings.Add($"[{template.BusinessName}] API {api.DisplayName} không khớp hàm nào trong code.");
                    continue;
                }
                if (!entries.Add(entry.Id)) continue;

                var graph = builder.Build(entry);
                if (graph.Truncated)
                    warnings.Add($"[{template.BusinessName}] Luồng {entry.Label} quá dài, đã cắt bớt (giới hạn {settings.MaxStepsPerFlow} bước / độ sâu {settings.MaxFlowDepth}).");

                var flow = new BusinessFlow
                {
                    Id = Guid.NewGuid(),
                    BusinessId = business.Id,
                    EntryMethodId = entry.Id,
                    Name = Truncate(api.DisplayName, 500)!,
                    MermaidGraph = MermaidBuilder.Build(graph)
                };
                flows.Add(flow);

                edges.AddRange(graph.Steps.Select(s => new DataFlowEdge
                {
                    Id = Guid.NewGuid(),
                    FlowId = flow.Id,
                    StepOrder = s.Order,
                    FromMethodId = s.From.Id,
                    ToMethodId = s.To.Id,
                    EdgeType = s.EdgeType,
                    ConditionContext = s.Condition,
                    DataLabel = s.DataLabel
                }));

                methodIds.UnionWith(graph.Methods.Select(m => m.Id));
            }

            if (entries.Count == 0)
                warnings.Add($"[{template.BusinessName}] Nghiệp vụ không có code: không API nào khớp được hàm trong repo.");

            businessMethods.AddRange(methodIds.Select(id => new BusinessMethod { BusinessId = business.Id, MethodSourceId = id }));
        }

        return new AnalysisModel(methods, businesses, businessMethods, flows, edges);
    }

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
