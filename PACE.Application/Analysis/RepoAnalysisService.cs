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
    ILogger<RepoAnalysisService> logger,
    ICurrentUserService? currentUser = null,
    ITemplateGenerationService? templateGen = null) : IRepoAnalysisService
{
    private sealed record ApiSync(Guid BusinessId, string? Description, List<BusinessApi> Apis);

    private sealed record AnalysisModel(
        List<MethodSource> Methods,
        List<Business> Businesses,
        List<BusinessMethod> BusinessMethods,
        List<BusinessFlow> Flows,
        List<DataFlowEdge> Edges,
        List<ApiSync> ApiSyncs);

    public async Task<AnalyzeResultDto> CreateAndAnalyzeAsync(CreateAnalysisRunRequest request, CancellationToken ct = default)
    {
        if (RepoUrlHelper.FindProblem(request.RepoUrl) is { } problem) throw new BadRequestException(problem);
        var confirmed = request.Businesses is { Count: > 0 } ? BusinessTemplateLoader.Normalize(request.Businesses) : null;
        var url = RepoUrlHelper.Normalize(request.RepoUrl);

        AnalysisRun? run = null;
        if (!request.ForceNew && !request.SourceRunId.HasValue)
        {
            run = await uow.AnalysisRuns.GetByRepoUrlAsync(url, ct);
        }

        var created = run is null;

        if (run is null)
        {
            string repoName;
            if (!string.IsNullOrWhiteSpace(request.RepoName))
            {
                repoName = request.RepoName.Trim();
            }
            else if (request.SourceRunId.HasValue)
            {
                var sourceRun = await uow.AnalysisRuns.GetByIdAsync(request.SourceRunId.Value, ct);
                var allNames = await uow.AnalysisRuns.GetAllRepoNamesAsync(ct);
                var baseName = sourceRun?.RepoName ?? RepoUrlHelper.DeriveName(url);
                repoName = RepoUrlHelper.ComputeCopyName(baseName, allNames);
            }
            else
            {
                repoName = RepoUrlHelper.DeriveName(url);
            }

            Guid? targetGroupId = request.GroupId;
            if (!targetGroupId.HasValue && request.ClassId.HasValue && currentUser?.IsAuthenticated == true)
            {
                if (currentUser.Role == UserRole.Student)
                {
                    targetGroupId = await uow.AnalysisRuns.GetStudentGroupIdInClassAsync(currentUser.UserId, request.ClassId.Value, ct);
                }
            }

            run = new AnalysisRun
            {
                Id = Guid.NewGuid(),
                RepoUrl = url,
                RepoName = repoName,
                GroupId = targetGroupId
            };
            await uow.AnalysisRuns.AddAsync(run, ct);
            await uow.SaveChangesAsync(ct);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.RepoName))
            {
                run.RepoName = request.RepoName.Trim();
            }

            Guid? targetGroupId = request.GroupId;
            if (!targetGroupId.HasValue && request.ClassId.HasValue && currentUser?.IsAuthenticated == true)
            {
                if (currentUser.Role == UserRole.Student)
                {
                    targetGroupId = await uow.AnalysisRuns.GetStudentGroupIdInClassAsync(currentUser.UserId, request.ClassId.Value, ct);
                }
            }

            if (targetGroupId.HasValue && !run.GroupId.HasValue)
            {
                run.GroupId = targetGroupId;
            }

            await uow.SaveChangesAsync(ct);
        }

        try
        {
            var result = await AnalyzeAsync(run.Id, confirmed, request.ReplaceExisting, ct);

            // Nếu phân tích thành công và có yêu cầu chuyển sinh viên từ repo nguồn sang bản copy mới
            if (created && request.SourceRunId.HasValue && request.MigrateStudents)
            {
                var students = await uow.Users.FindAsync(
                    u => u.AnalysisRunId == request.SourceRunId.Value && u.Role == UserRole.Student,
                    asNoTracking: false,
                    ct);
                foreach (var s in students)
                {
                    s.AnalysisRunId = run.Id;
                    uow.Users.Update(s);
                }
                await uow.SaveChangesAsync(ct);
            }

            return result;
        }
        catch
        {
            if (created) await RemoveRunAsync(run.Id);
            throw;
        }
    }

    public Task<AnalyzeResultDto> ReanalyzeAsync(Guid analysisRunId, bool regenerateTemplate = false, CancellationToken ct = default)
        => AnalyzeAsync(analysisRunId, null, true, ct, regenerateTemplate);

    /// <summary>
    /// Tải lại source sau lần phân tích đầu: chỉ cập nhật danh sách hàm / API theo code mới.
    /// Không gọi Gemini, không tạo lại template, không dựng lại luồng; nghiệp vụ, API đã gán, luồng, sơ đồ
    /// và câu hỏi giữ nguyên. API mới hiện trong danh sách API để nhóm tự thêm vào nghiệp vụ.
    /// Repo chưa phân tích lần nào thì chạy phân tích đầy đủ.
    /// </summary>
    public async Task<AnalyzeResultDto> RefreshApisAsync(Guid analysisRunId, CancellationToken ct = default)
    {
        var run = await uow.AnalysisRuns.GetByIdAsync(analysisRunId, ct) ?? throw new NotFoundException("repo", analysisRunId);
        if (string.IsNullOrWhiteSpace(run.RepoUrl)) throw new BadRequestException("Repo chưa có link GitHub.");
        if (run.IsArchived) throw new BadRequestException("Không thể phân tích lại bản lịch sử.");

        if (!await uow.MethodSources.AnyAsync(m => m.AnalysisRunId == analysisRunId, ct))
            return await ReanalyzeAsync(analysisRunId, regenerateTemplate: true, ct);

        if (!AnalysisRunLock.TryAcquire(analysisRunId))
            throw new ConflictException("Repo này đang được phân tích, vui lòng đợi lần chạy trước xong.");

        var warnings = new List<string>();
        try
        {
            using var clone = await git.CloneAsync(run.RepoUrl, ct);

            var scan = scanner.Scan(clone.WorkingPath, ct);
            warnings.AddRange(scan.Warnings);
            warnings.AddRange(scan.RedactionWarnings());
            LogScan(run.RepoUrl, scan);
            if (scan.TotalFiles == 0)
                throw new UnprocessableException("Không tìm thấy mã nguồn được hỗ trợ trong repo.");

            var parsed = new ParseResult();
            foreach (var parser in parsers)
                parsed.Merge(parser.Parse(scan.FilesFor(parser.Kind), ct));
            parsed.Methods.AddRange(scan.FrontendEntries());
            warnings.AddRange(parsed.Warnings);

            if (parsed.Methods.Count == 0)
                throw new UnprocessableException("Parse xong nhưng không tìm thấy hàm nào trong mã nguồn.");

            var sync = await uow.ExecuteInTransactionAsync(async token =>
            {
                // Tải lại trong transaction: lần retry của execution strategy chạy sau khi change tracker đã bị xoá.
                var existing = await uow.MethodSources.FindAsync(m => m.AnalysisRunId == analysisRunId, asNoTracking: false, token);
                var fresh = parsed.Methods.Select(m => ToMethodSource(analysisRunId, m)).ToList();
                var result = SyncMethodSources(existing, fresh);

                // Hàm không còn trong code: chỉ xoá khi không có luồng nào dùng, để luồng và câu hỏi cũ không bị hỏng.
                var keptOld = 0;
                if (result.Missing.Count > 0)
                {
                    var missingIds = result.Missing.Select(m => m.Id).ToList();
                    var used = (await uow.BusinessMethods.FindAsync(bm => missingIds.Contains(bm.MethodSourceId), ct: token))
                        .Select(bm => bm.MethodSourceId)
                        .Concat((await uow.DataFlowEdges.FindAsync(e => missingIds.Contains(e.FromMethodId) || missingIds.Contains(e.ToMethodId), ct: token))
                            .SelectMany(e => new[] { e.FromMethodId, e.ToMethodId }))
                        .ToHashSet();
                    var removable = result.Missing.Where(m => !used.Contains(m.Id)).ToList();
                    keptOld = result.Missing.Count - removable.Count;
                    uow.MethodSources.RemoveRange(removable);
                }

                await uow.MethodSources.AddRangeAsync(result.Added, token);

                var trackedRun = await uow.AnalysisRuns.GetByIdAsync(analysisRunId, token) ?? throw new NotFoundException("repo", analysisRunId);
                trackedRun.AnalyzedRepoUrl = run.RepoUrl;
                trackedRun.AnalyzedCommitSha = clone.CommitSha;
                trackedRun.AnalyzedAt = DateTime.UtcNow;

                return (Total: fresh.Count, result.Added, result.Missing, KeptOld: keptOld);
            }, ct);

            var newApis = sync.Added.Count(IsApiMethod);
            var removedApis = sync.Missing.Count(IsApiMethod);
            warnings.Insert(0, newApis == 0
                ? "Không có API mới. Nghiệp vụ và luồng xử lý giữ nguyên."
                : $"Có {newApis} API mới. Mở nghiệp vụ, bấm sửa để thêm API vào nghiệp vụ phù hợp.");
            if (removedApis > 0)
                warnings.Add($"{removedApis} API không còn trong code. API đã gán vào nghiệp vụ vẫn được giữ, nhóm tự gỡ nếu không dùng nữa.");
            if (sync.KeptOld > 0)
                warnings.Add($"{sync.KeptOld} hàm không còn trong code nhưng vẫn giữ lại vì luồng xử lý cũ đang dùng.");

            logger.LogInformation("Tải lại API {RepoUrl}: {Methods} hàm, thêm {Added}, không còn {Missing}",
                run.RepoUrl, sync.Total, sync.Added.Count, sync.Missing.Count);

            return new AnalyzeResultDto(run.Id, string.Join(", ", scan.Stacks), sync.Total,
                await uow.Businesses.CountAsync(b => b.AnalysisRunId == analysisRunId, ct),
                await uow.BusinessFlows.CountAsync(f => f.Business.AnalysisRunId == analysisRunId, ct),
                0, warnings, newApis);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tải lại API của repo {RunId} thất bại", analysisRunId);
            throw;
        }
        finally
        {
            AnalysisRunLock.Release(analysisRunId);
        }
    }

    private sealed record MethodSync(List<MethodSource> Added, List<MethodSource> Missing);

    /// <summary>
    /// Ghép hàm vừa parse với hàm đang lưu theo class + tên + tham số (ưu tiên cùng file) để giữ nguyên Id:
    /// hàm cũ được cập nhật code, hàm mới được thêm, hàm không còn được trả về để xử lý riêng.
    /// </summary>
    private static MethodSync SyncMethodSources(List<MethodSource> existing, List<MethodSource> fresh)
    {
        static string Key(MethodSource m) => $"{m.ClassName}|{m.MethodName}|{m.Parameters}";

        var pool = existing.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToList());
        var added = new List<MethodSource>();
        foreach (var m in fresh)
        {
            if (!pool.TryGetValue(Key(m), out var candidates) || candidates.Count == 0)
            {
                added.Add(m);
                continue;
            }

            var match = candidates.FirstOrDefault(c => c.FilePath == m.FilePath) ?? candidates[0];
            candidates.Remove(match);
            match.FilePath = m.FilePath;
            match.HttpVerb = m.HttpVerb;
            match.Route = m.Route;
            match.SourceCode = m.SourceCode;
            match.Language = m.Language;
            match.NodeType = m.NodeType;
        }

        return new MethodSync(added, pool.Values.SelectMany(v => v).ToList());
    }

    private static bool IsApiMethod(MethodSource m)
        => !m.ClassName.StartsWith(RepositoryScan.FrontendClassPrefix, StringComparison.Ordinal)
           && (m.HttpVerb != null || m.ClassName.EndsWith("Controller", StringComparison.Ordinal));

    private static MethodSource ToMethodSource(Guid runId, ParsedMethod m) => new()
    {
        Id = m.Id,
        AnalysisRunId = runId,
        FilePath = Truncate(m.FilePath, 500),
        ClassName = Truncate(m.ClassName, 255)!,
        MethodName = Truncate(m.MethodName, 255)!,
        Parameters = m.Parameters.Count == 0 ? null : string.Join(", ", m.Parameters.Select(p => p.ToString())),
        HttpVerb = Truncate(m.HttpVerb, 10),
        Route = Truncate(m.Route, 500),
        SourceCode = m.SourceCode,
        Language = m.Language,
        NodeType = m.IsDecision ? NodeType.DecisionGateway : NodeType.Activity
    };

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
    private void LogScan(string repoUrl, RepositoryScan scan)
    {
        var byRule = string.Join(", ", scan.Redactions.GroupBy(r => r.Rule).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}"));
        logger.LogInformation(
            "Quét {RepoUrl}: nhận {Accepted} file, bỏ qua {Ignored}, lỗi {Failed}; đã che {Redactions} thông tin nhạy cảm ({Rules})",
            repoUrl, scan.AcceptedCount, scan.IgnoredCount, scan.FailedCount, scan.Redactions.Count, byRule.Length == 0 ? "không có" : byRule);
        foreach (var failed in scan.FileLog.Where(e => e.Status == ScanFileStatus.Failed))
            logger.LogWarning("Quét {RepoUrl}: file {Path} bị bỏ: {Reason}", repoUrl, failed.RelativePath, failed.Reason);
        foreach (var hit in scan.Redactions.Take(50))
            logger.LogInformation("Đã che [{Rule}] tại {Path}:{Line}", hit.Rule, hit.RelativePath, hit.Line);
    }

    private async Task<AnalyzeResultDto> AnalyzeAsync(Guid analysisRunId, List<BusinessTemplate>? confirmed, bool replaceExisting, CancellationToken ct,
        bool regenerateTemplate = false)
    {
        var run = await uow.AnalysisRuns.GetByIdAsync(analysisRunId, ct) ?? throw new NotFoundException("repo", analysisRunId);
        if (string.IsNullOrWhiteSpace(run.RepoUrl)) throw new BadRequestException("Repo chưa có link GitHub.");
        if (run.IsArchived) throw new BadRequestException("Không thể phân tích lại bản lịch sử.");

        if (!AnalysisRunLock.TryAcquire(analysisRunId))
            throw new ConflictException("Repo này đang được phân tích, vui lòng đợi lần chạy trước xong.");

        var warnings = new List<string>();
        try
        {
            using var clone = await git.CloneAsync(run.RepoUrl, ct);

            // Ưu tiên: template gửi kèm request > template admin đã chốt trong DB > file trong repo.
            var existing = await uow.Businesses.GetWithApisByRepoAsync(analysisRunId, ct);
            var templates = confirmed;
            var templateSource = BusinessTemplateSource.Confirmed;
            var syncApis = true;
            IReadOnlySet<Guid>? resyncBusinessIds = null;
            if (templates is null && (existing.Any(b => b.Apis.Count > 0) || (regenerateTemplate && existing.Count > 0)))
            {
                templates = BusinessTemplateLoader.Normalize(DeclaredBusinessTemplate.Build(existing));
                syncApis = false;

                // Tải lại source: tạo lại template từ code mới rồi gộp vào nghiệp vụ đang có, không xoá gì.
                if (regenerateTemplate)
                {
                    var merged = await RegenerateAndMergeAsync(run.RepoUrl, clone, templates, warnings, ct);
                    if (merged is not null)
                    {
                        templates = merged.Templates;
                        resyncBusinessIds = merged.ChangedBusinessIds;
                    }
                }
            }
            if (templates is null && !regenerateTemplate && run is { TemplateSource: BusinessTemplateSource.Confirmed, BusinessTemplateJson: not null })
                templates = BusinessTemplateLoader.Parse(run.BusinessTemplateJson);
            if (templates is null)
            {
                var templateJson = await scanner.ReadBusinessTemplateAsync([clone.WorkingPath, clone.RootPath], ct);
                if (templateJson != null)
                {
                    templates = BusinessTemplateLoader.Parse(templateJson);
                    templateSource = BusinessTemplateSource.Repository;
                }
                else if (templateGen != null)
                {
                    // Tự động quét và tạo template nghiệp vụ nếu repo chưa có template_business.json
                    var gen = await templateGen.GenerateAsync(new GenerateTemplateRequest { RepoUrl = run.RepoUrl, UseAi = true, Regenerate = regenerateTemplate, GroupMissingByController = false }, ct);
                    templates = gen.Businesses.ToList();
                    templateSource = gen.Source;
                }
                else
                {
                    throw new BadRequestException($"Repo không có file {BusinessTemplateLoader.FileName}. Gọi POST /api/templates/generate để tạo, chỉnh rồi gửi kèm businesses.");
                }
            }

            var scan = scanner.Scan(clone.WorkingPath, ct);
            warnings.AddRange(scan.Warnings);
            warnings.AddRange(scan.RedactionWarnings());
            LogScan(run.RepoUrl, scan);
            if (scan.TotalFiles == 0)
                throw new UnprocessableException("Không tìm thấy mã nguồn được hỗ trợ trong repo.");

            var parsed = new ParseResult();
            foreach (var parser in parsers)
                parsed.Merge(parser.Parse(scan.FilesFor(parser.Kind), ct));
            parsed.Methods.AddRange(scan.FrontendEntries());
            warnings.AddRange(parsed.Warnings);

            if (parsed.Methods.Count == 0)
                throw new UnprocessableException("Parse xong nhưng không tìm thấy hàm nào trong mã nguồn.");

            var model = BuildModel(run.Id, templates, parsed, warnings, existing, syncApis, resyncBusinessIds);

            await uow.ExecuteInTransactionAsync(async token =>
            {
                await uow.MethodSources.DeleteByRepoAsync(analysisRunId, token);
                foreach (var sync in model.ApiSyncs)
                    await uow.Businesses.SyncApisAsync(sync.BusinessId, sync.Description, sync.Apis, token);

                await uow.MethodSources.AddRangeAsync(model.Methods, token);
                await uow.Businesses.AddRangeAsync(model.Businesses, token);
                await uow.BusinessMethods.AddRangeAsync(model.BusinessMethods, token);
                await uow.BusinessFlows.AddRangeAsync(model.Flows, token);
                await uow.DataFlowEdges.AddRangeAsync(model.Edges, token);

                // Tải lại trong transaction: lần retry của execution strategy chạy sau khi change tracker đã bị xoá.
                var trackedRun = await uow.AnalysisRuns.GetByIdAsync(analysisRunId, token) ?? throw new NotFoundException("repo", analysisRunId);
                StoredBusinessTemplate.Save(trackedRun, templates, templateSource);
                trackedRun.AnalyzedRepoUrl = run.RepoUrl;
                trackedRun.AnalyzedCommitSha = clone.CommitSha;
                trackedRun.AnalyzedAt = DateTime.UtcNow;
            }, ct);

            logger.LogInformation("Phân tích {RepoUrl}: {Methods} hàm, {Businesses} nghiệp vụ, {Flows} luồng",
                run.RepoUrl, model.Methods.Count, templates.Count, model.Flows.Count);

            return new AnalyzeResultDto(run.Id, string.Join(", ", scan.Stacks), model.Methods.Count, templates.Count,
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

    /// <summary>
    /// Tạo lại template nghiệp vụ từ code vừa kéo về rồi gộp vào nghiệp vụ hiện có.
    /// Ưu tiên file template_business.json trong repo nếu nhóm có commit file này, không có thì quét code (Gemini Hybrid).
    /// Lỗi khi tạo lại không làm hỏng lần tải lại: giữ nguyên nghiệp vụ cũ và báo cảnh báo.
    /// </summary>
    private async Task<BusinessTemplateMerger.Result?> RegenerateAndMergeAsync(
        string repoUrl, IClonedRepository clone, List<BusinessTemplate> current, List<string> warnings, CancellationToken ct)
    {
        try
        {
            List<BusinessTemplate>? generated = null;
            var templateJson = await scanner.ReadBusinessTemplateAsync([clone.WorkingPath, clone.RootPath], ct);
            if (templateJson is not null)
            {
                try
                {
                    generated = BusinessTemplateLoader.Parse(templateJson);
                }
                catch (BadRequestException ex)
                {
                    warnings.Add($"File {BusinessTemplateLoader.FileName} trong repo không dùng được ({ex.Message}), chuyển sang quét code.");
                }
            }

            if (generated is null)
            {
                if (templateGen is null) return null;
                var gen = await templateGen.GenerateAsync(new GenerateTemplateRequest { RepoUrl = repoUrl, UseAi = true, Regenerate = true, GroupMissingByController = false }, ct);
                warnings.AddRange(gen.Warnings);
                generated = gen.Businesses.ToList();
            }

            var merged = BusinessTemplateMerger.Merge(current, generated);
            warnings.AddRange(merged.Notes);
            if (merged.AddedApiCount == 0)
                warnings.Add("Đã tạo lại template từ code mới: không có API mới, nghiệp vụ giữ nguyên.");
            return merged;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Tạo lại template cho {RepoUrl} thất bại", repoUrl);
            warnings.Add($"Không tạo lại được template từ code mới ({ex.Message}), nghiệp vụ giữ nguyên.");
            return null;
        }
    }

    private AnalysisModel BuildModel(Guid runId, List<BusinessTemplate> templates, ParseResult parsed, List<string> warnings,
        IReadOnlyList<Business> existing, bool syncApis, IReadOnlySet<Guid>? resyncBusinessIds = null)
    {
        var settings = options.Value;
        var resolver = new CallResolver(parsed.Classes, parsed.Methods);
        var matcher = new EntryPointMatcher(parsed.Methods);
        var builder = new FlowBuilder(resolver, settings.MaxFlowDepth, settings.MaxStepsPerFlow);

        var frontendMethods = parsed.Methods
            .Where(m => m.ClassName.StartsWith(RepositoryScan.FrontendClassPrefix, StringComparison.Ordinal))
            .ToList();
        var methods = parsed.Methods.Select(m => ToMethodSource(runId, m)).ToList();

        var businesses = new List<Business>();
        var businessMethods = new List<BusinessMethod>();
        var flows = new List<BusinessFlow>();
        var edges = new List<DataFlowEdge>();
        var apiSyncs = new List<ApiSync>();
        var usedBusinessIds = new HashSet<Guid>();
        var nextOrder = existing.Count == 0 ? 0 : existing.Max(b => b.OrderIndex) + 1;

        foreach (var template in templates)
        {
            var current = (template.BusinessId is { } templateBusinessId
                    ? existing.FirstOrDefault(b => b.Id == templateBusinessId && !usedBusinessIds.Contains(b.Id))
                    : null)
                ?? existing.FirstOrDefault(b => !usedBusinessIds.Contains(b.Id)
                    && string.Equals(b.BusinessName.Trim(), template.BusinessName, StringComparison.OrdinalIgnoreCase));
            // Nghiệp vụ mới luôn mang API của template; nghiệp vụ cũ chỉ đồng bộ API khi syncApis hoặc vừa được gộp thêm API mới.
            var apis = ToBusinessApis(template);
            var shouldSync = current is not null && (syncApis || resyncBusinessIds?.Contains(current.Id) == true);
            var business = current ?? new Business
            {
                Id = Guid.NewGuid(),
                AnalysisRunId = runId,
                BusinessName = template.BusinessName,
                Description = template.Description,
                OrderIndex = nextOrder++,
                Apis = apis
            };
            usedBusinessIds.Add(business.Id);
            if (current is null) businesses.Add(business);
            else if (shouldSync) apiSyncs.Add(new ApiSync(current.Id, syncApis ? template.Description : current.Description, apis));

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
                foreach (var screen in frontendMethods.Where(screen =>
                    (api.Path is { Length: > 1 } path && screen.SourceCode.Contains(path, StringComparison.Ordinal))
                    || (entry.ClassName.StartsWith(RepositoryScan.FrontendClassPrefix, StringComparison.Ordinal)
                        && string.Equals(Path.GetExtension(screen.FilePath), ".html", StringComparison.OrdinalIgnoreCase)
                        && screen.SourceCode.Contains(entry.FilePath.Split('/').Last(), StringComparison.Ordinal))))
                    methodIds.Add(screen.Id);
            }

            if (entries.Count == 0)
                warnings.Add($"[{template.BusinessName}] Nghiệp vụ không có code: không API nào khớp được hàm trong repo.");

            businessMethods.AddRange(methodIds.Select(id => new BusinessMethod { BusinessId = business.Id, MethodSourceId = id }));
        }

        return new AnalysisModel(methods, businesses, businessMethods, flows, edges, apiSyncs);
    }

    private static List<BusinessApi> ToBusinessApis(BusinessTemplate template)
        => template.Apis
            .GroupBy(a => (a.Controller.Trim().ToLowerInvariant(), a.ResolveFunctionName().ToLowerInvariant()))
            .Select((g, index) =>
            {
                var api = g.First();
                return new BusinessApi
                {
                    Controller = Truncate(api.Controller.Trim(), 255)!,
                    Function = Truncate(api.ResolveFunctionName(), 255)!,
                    HttpMethod = Truncate(api.ResolveHttpVerb(), 10),
                    Path = Truncate(api.Path?.Trim(), 500),
                    OrderIndex = index
                };
            })
            .ToList();

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}
