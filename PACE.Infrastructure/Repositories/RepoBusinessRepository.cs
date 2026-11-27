using Microsoft.EntityFrameworkCore;
using PACE.Application.Analysis.Models;
using PACE.Application.Common.ReadModels;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class RepoBusinessRepository(AppDbContext context) : IRepoBusinessRepository
{
    public Task<RepoAccessInfo?> GetAccessInfoAsync(Guid analysisRunId, CancellationToken ct = default)
        => context.AnalysisRuns.AsNoTracking()
            .Where(r => r.Id == analysisRunId)
            .Select(r => new RepoAccessInfo(r.Id, r.GroupId, r.IsArchived, r.RepoUrl, r.AnalyzedAt, r.AnalyzedCommitSha))
            .FirstOrDefaultAsync(ct);

    public async Task<string?> GetStudentCodeAsync(Guid userId, CancellationToken ct = default)
    {
        var profileCode = await context.StudentProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.StudentCode)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(profileCode)) return profileCode;

        return await context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.StudentCode)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> IsGroupMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        var studentCode = await GetStudentCodeAsync(userId, ct);
        return await context.GroupMembers.AnyAsync(m => m.GroupId == groupId
            && (m.StudentProfile.UserId == userId || (studentCode != null && m.StudentCode == studentCode)), ct);
    }

    public Task<bool> IsStudentInGroupAsync(Guid groupId, string studentCode, CancellationToken ct = default)
        => context.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.StudentCode == studentCode, ct);

    public async Task<bool> IsGroupTopicApprovedAsync(Guid groupId, CancellationToken ct = default)
    {
        if (await context.Groups.AnyAsync(g => g.Id == groupId && g.CourseTopicId != null, ct)) return true;

        var latestStatus = await context.GroupTopicSubmissions.AsNoTracking()
            .Where(s => s.GroupId == groupId)
            .OrderByDescending(s => s.Version)
            .Select(s => (GroupTopicStatus?)s.Status)
            .FirstOrDefaultAsync(ct);
        return latestStatus == GroupTopicStatus.Approved;
    }

    public Task<List<RepoApiMethod>> GetApiMethodsAsync(Guid analysisRunId, CancellationToken ct = default)
        => context.MethodSources.AsNoTracking()
            .Where(m => m.AnalysisRunId == analysisRunId
                && !m.ClassName.StartsWith(RepositoryScan.FrontendClassPrefix)
                && (m.HttpVerb != null || m.ClassName.EndsWith("Controller")))
            .OrderBy(m => m.ClassName)
            .ThenBy(m => m.MethodName)
            .Select(m => new RepoApiMethod(m.Id, m.ClassName, m.MethodName, m.HttpVerb, m.Route, m.FilePath))
            .ToListAsync(ct);

    public Task<List<Business>> GetBusinessesWithApisAsync(Guid analysisRunId, CancellationToken ct = default)
        => context.Businesses.AsNoTracking()
            .Include(b => b.Apis)
            .Where(b => b.AnalysisRunId == analysisRunId)
            .OrderBy(b => b.OrderIndex)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);

    public Task<List<BusinessDiagram>> GetDiagramSummariesAsync(Guid analysisRunId, CancellationToken ct = default)
        => context.BusinessDiagrams.AsNoTracking()
            .Where(d => d.Business.AnalysisRunId == analysisRunId)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new BusinessDiagram
            {
                Id = d.Id,
                BusinessId = d.BusinessId,
                Title = d.Title,
                Format = d.Format,
                SpecificationJson = d.SpecificationJson,
                PreviewUrl = d.PreviewUrl,
                Description = d.Description,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync(ct);

    public Task<Dictionary<Guid, BusinessCounts>> GetBusinessCountsAsync(Guid analysisRunId, CancellationToken ct = default)
        => context.Businesses.AsNoTracking()
            .Where(b => b.AnalysisRunId == analysisRunId)
            .Select(b => new BusinessCounts(b.Id, b.BusinessMethods.Count, b.Flows.Count, b.Questions.Count))
            .ToDictionaryAsync(c => c.BusinessId, ct);

    public Task<Business?> GetBusinessForUpdateAsync(Guid analysisRunId, Guid businessId, CancellationToken ct = default)
        => context.Businesses
            .Include(b => b.Apis)
            .FirstOrDefaultAsync(b => b.Id == businessId && b.AnalysisRunId == analysisRunId, ct);

    public Task<bool> BusinessExistsAsync(Guid analysisRunId, Guid businessId, CancellationToken ct = default)
        => context.Businesses.AnyAsync(b => b.Id == businessId && b.AnalysisRunId == analysisRunId, ct);

    public Task<bool> BusinessHasQuestionsAsync(Guid businessId, CancellationToken ct = default)
        => context.GeneratedQuestions.AnyAsync(q => q.BusinessId == businessId, ct);

    public async Task<int> GetNextOrderIndexAsync(Guid analysisRunId, CancellationToken ct = default)
    {
        var max = await context.Businesses
            .Where(b => b.AnalysisRunId == analysisRunId)
            .MaxAsync(b => (int?)b.OrderIndex, ct);
        return max is null ? 0 : max.Value + 1;
    }

    public Task<BusinessDiagram?> GetDiagramAsync(Guid businessId, Guid diagramId, bool tracking, CancellationToken ct = default)
    {
        var query = tracking ? context.BusinessDiagrams : context.BusinessDiagrams.AsNoTracking();
        return query.FirstOrDefaultAsync(d => d.Id == diagramId && d.BusinessId == businessId, ct);
    }

    public Task<bool> DiagramTitleExistsAsync(Guid businessId, string title, Guid? exceptId, CancellationToken ct = default)
    {
        var normalized = title.Trim().ToLower();
        return context.BusinessDiagrams.AnyAsync(d => d.BusinessId == businessId
            && d.Id != exceptId
            && d.Title.ToLower() == normalized, ct);
    }

    public void AddBusiness(Business business) => context.Businesses.Add(business);

    public void RemoveBusiness(Business business) => context.Businesses.Remove(business);

    public void AddApi(BusinessApi api) => context.BusinessApis.Add(api);

    public void RemoveApi(BusinessApi api) => context.BusinessApis.Remove(api);

    public void AddDiagram(BusinessDiagram diagram) => context.BusinessDiagrams.Add(diagram);

    public void RemoveDiagram(BusinessDiagram diagram) => context.BusinessDiagrams.Remove(diagram);
}
