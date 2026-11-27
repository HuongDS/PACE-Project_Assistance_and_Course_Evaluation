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

    private async Task<AnalyzeResultDto> AnalyzeAsync(Guid analysisRunId, List<BusinessTemplate>? confirmed, bool replaceExisting, CancellationToken ct)
    {
        var run = await uow.AnalysisRuns.GetByIdAsync(analysisRunId, ct) ?? throw new NotFoundException("repo", analysisRunId);
        if (string.IsNullOrWhiteSpace(run.RepoUrl)) throw new BadRequestException("Repo chưa có link GitHub.");
        if (run.IsArchived) throw new BadRequestException("Không thể phân tích lại bản lịch sử.");
        if (!replaceExisting && await uow.AnalysisRuns.HasQuestionsAsync(analysisRunId, ct))
            throw new ConflictException("Repo đã có câu hỏi. Hãy xác nhận cập nhật nghiệp vụ để phân tích lại và lưu dữ liệu cũ vào lịch sử.");

        if (!AnalysisRunLock.TryAcquire(analysisRunId))
            throw new ConflictException("Repo này đang được phân tích, vui lòng đợi lần chạy trước xong.");

        var warnings = new List<string>();
        try
        {
            using var clone = await git.CloneAsync(run.RepoUrl, ct);

            // Ưu tiên: template gửi kèm request > template admin đã chốt trong DB > file trong repo.
            var templates = confirmed;
            var templateSource = BusinessTemplateSource.Confirmed;
            if (templates is null && run is { TemplateSource: BusinessTemplateSource.Confirmed, BusinessTemplateJson: not null })
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
                    var gen = await templateGen.GenerateAsync(new GenerateTemplateRequest { RepoUrl = run.RepoUrl, UseAi = true }, ct);
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

            var model = BusinessModelBuilder.Build(run.Id, templates, parsed, options.Value, warnings);

            await uow.ExecuteInTransactionAsync(async token =>
            {
                if (replaceExisting) await uow.AnalysisRuns.ArchiveCurrentAsync(analysisRunId, token);
                else if (await uow.AnalysisRuns.HasQuestionsAsync(analysisRunId, token))
                    throw new ConflictException("Repo vừa có thêm câu hỏi. Vui lòng xác nhận phân tích lại.");
                await uow.Businesses.DeleteByRepoAsync(analysisRunId, token);
                await uow.MethodSources.DeleteByRepoAsync(analysisRunId, token);

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

    /// <summary>Ghi log kết quả quét: số file nhận/bỏ/lỗi, số chỗ đã che theo luật. Không ghi giá trị bí mật.</summary>
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
}
