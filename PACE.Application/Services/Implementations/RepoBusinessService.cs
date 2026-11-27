using System.Text.Json;
using System.Xml;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.ReadModels;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.QuestionGeneration.Drawio;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class RepoBusinessService(
    IUnitOfWork uow,
    ICurrentUserService currentUser,
    IRepoAnalysisService analysisService) : IRepoBusinessService
{
    private const int MaxSourceXmlLength = 2_000_000;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private sealed record RepoAccess(RepoAccessInfo Info, bool CanEdit);

    private sealed record EditCheck(bool Allowed, string Reason);

    public async Task<RepoBusinessWorkspaceDto> GetWorkspaceAsync(Guid analysisRunId, CancellationToken ct = default)
    {
        var access = await GetAccessAsync(analysisRunId, false, ct);
        var businesses = await BuildBusinessDtosAsync(analysisRunId, ct);
        return new RepoBusinessWorkspaceDto(
            analysisRunId,
            access.Info.GroupId,
            access.CanEdit,
            access.Info.AnalyzedAt,
            access.Info.AnalyzedCommitSha,
            businesses);
    }

    public async Task<IReadOnlyList<RepoApiDto>> GetApisAsync(Guid analysisRunId, CancellationToken ct = default)
    {
        await GetAccessAsync(analysisRunId, false, ct);
        var methods = await uow.RepoBusinesses.GetApiMethodsAsync(analysisRunId, ct);
        var businesses = await uow.RepoBusinesses.GetBusinessesWithApisAsync(analysisRunId, ct);
        return methods
            .Select(m => new RepoApiDto(
                m.Id,
                m.ClassName,
                m.MethodName,
                m.HttpVerb,
                m.Route,
                m.FilePath,
                businesses.Where(b => b.Apis.Any(a => Matches(a, m))).Select(b => b.Id).ToList()))
            .ToList();
    }

    public async Task<AnalyzeResultDto> ReloadSourceAsync(Guid analysisRunId, CancellationToken ct = default)
    {
        await GetAccessAsync(analysisRunId, !currentUser.IsAdmin, ct);
        return await analysisService.RefreshApisAsync(analysisRunId, ct);
    }

    public async Task<RepoBusinessDto> GetBusinessAsync(Guid analysisRunId, Guid businessId, CancellationToken ct = default)
    {
        await GetAccessAsync(analysisRunId, false, ct);
        return await GetBusinessDtoAsync(analysisRunId, businessId, ct);
    }

    public async Task<RepoBusinessDto> CreateBusinessAsync(Guid analysisRunId, SaveRepoBusinessRequest request, CancellationToken ct = default)
    {
        var access = await GetAccessAsync(analysisRunId, true, ct);
        var name = RequireText(request.BusinessName, "Tên nghiệp vụ");
        if (await uow.Businesses.NameExistsAsync(analysisRunId, name, null, ct))
            throw new ConflictException($"Repo đã có nghiệp vụ tên \"{name}\".");

        var currentStudentCode = await uow.RepoBusinesses.GetStudentCodeAsync(currentUser.UserId, ct);
        var owner = await ResolveOwnerAsync(access.Info, request.OwnerStudentCode, currentStudentCode, ct);
        var apis = await ResolveApisAsync(analysisRunId, request.MethodSourceIds, ct);
        var now = DateTime.UtcNow;

        var business = new Business
        {
            Id = Guid.NewGuid(),
            AnalysisRunId = analysisRunId,
            BusinessName = name,
            Description = Clean(request.Description),
            OrderIndex = request.OrderIndex ?? await uow.RepoBusinesses.GetNextOrderIndexAsync(analysisRunId, ct),
            OwnerStudentCode = owner,
            CreatedAt = now,
            UpdatedAt = now
        };
        uow.RepoBusinesses.AddBusiness(business);
        foreach (var api in apis)
        {
            api.BusinessId = business.Id;
            uow.RepoBusinesses.AddApi(api);
        }

        await uow.SaveChangesAsync(ct);
        return await GetBusinessDtoAsync(analysisRunId, business.Id, ct);
    }

    public async Task<RepoBusinessDto> UpdateBusinessAsync(Guid analysisRunId, Guid businessId, SaveRepoBusinessRequest request, CancellationToken ct = default)
    {
        var access = await GetAccessAsync(analysisRunId, true, ct);
        var business = await uow.RepoBusinesses.GetBusinessForUpdateAsync(analysisRunId, businessId, ct)
            ?? throw new NotFoundException("nghiệp vụ", businessId);

        var name = RequireText(request.BusinessName, "Tên nghiệp vụ");
        if (await uow.Businesses.NameExistsAsync(analysisRunId, name, businessId, ct))
            throw new ConflictException($"Repo đã có nghiệp vụ tên \"{name}\".");

        var keepIds = request.KeepApiIds.ToHashSet();
        var unknownKeepIds = keepIds.Where(id => business.Apis.All(a => a.Id != id)).ToList();
        if (unknownKeepIds.Count > 0)
            throw new BadRequestException($"API cần giữ không thuộc nghiệp vụ này: {string.Join(", ", unknownKeepIds)}");

        var desired = await ResolveApisAsync(analysisRunId, request.MethodSourceIds, ct);
        var desiredKeys = desired.Select(Key).ToHashSet();

        foreach (var api in business.Apis.ToList())
        {
            if (desiredKeys.Contains(Key(api)) || keepIds.Contains(api.Id)) continue;
            business.Apis.Remove(api);
            uow.RepoBusinesses.RemoveApi(api);
        }

        foreach (var api in desired)
        {
            var current = business.Apis.FirstOrDefault(a => Key(a) == Key(api));
            if (current is null)
            {
                api.BusinessId = business.Id;
                uow.RepoBusinesses.AddApi(api);
                continue;
            }

            current.HttpMethod = api.HttpMethod;
            current.Path = api.Path;
            current.OrderIndex = api.OrderIndex;
        }

        business.BusinessName = name;
        business.Description = Clean(request.Description);
        business.OwnerStudentCode = await ResolveOwnerAsync(access.Info, request.OwnerStudentCode, business.OwnerStudentCode, ct);
        if (request.OrderIndex is { } orderIndex) business.OrderIndex = orderIndex;
        business.UpdatedAt = DateTime.UtcNow;

        await uow.SaveChangesAsync(ct);
        return await GetBusinessDtoAsync(analysisRunId, businessId, ct);
    }

    public async Task DeleteBusinessAsync(Guid analysisRunId, Guid businessId, CancellationToken ct = default)
    {
        await GetAccessAsync(analysisRunId, true, ct);
        var business = await uow.RepoBusinesses.GetBusinessForUpdateAsync(analysisRunId, businessId, ct)
            ?? throw new NotFoundException("nghiệp vụ", businessId);
        if (await uow.RepoBusinesses.BusinessHasQuestionsAsync(businessId, ct))
            throw new ConflictException("Nghiệp vụ đã có câu hỏi nên không xoá được, để giữ lại câu trả lời của sinh viên.");

        uow.RepoBusinesses.RemoveBusiness(business);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<BusinessDiagramDto> GetDiagramAsync(Guid analysisRunId, Guid businessId, Guid diagramId, CancellationToken ct = default)
    {
        await GetAccessAsync(analysisRunId, false, ct);
        await EnsureBusinessAsync(analysisRunId, businessId, ct);
        var diagram = await uow.RepoBusinesses.GetDiagramAsync(businessId, diagramId, false, ct)
            ?? throw new NotFoundException("diagram", diagramId);
        return ToDetailDto(diagram);
    }

    public async Task<BusinessDiagramDto> CreateDiagramAsync(Guid analysisRunId, Guid businessId, SaveBusinessDiagramRequest request, CancellationToken ct = default)
    {
        await GetAccessAsync(analysisRunId, true, ct);
        await EnsureBusinessAsync(analysisRunId, businessId, ct);

        var title = RequireText(request.Title, "Tên diagram");
        if (await uow.RepoBusinesses.DiagramTitleExistsAsync(businessId, title, null, ct))
            throw new ConflictException($"Nghiệp vụ đã có diagram tên \"{title}\".");

        var now = DateTime.UtcNow;
        var diagram = new BusinessDiagram
        {
            BusinessId = businessId,
            Title = title,
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplyContent(diagram, request, businessId);
        uow.RepoBusinesses.AddDiagram(diagram);

        await uow.SaveChangesAsync(ct);
        return ToDetailDto(diagram);
    }

    public async Task<BusinessDiagramDto> UpdateDiagramAsync(Guid analysisRunId, Guid businessId, Guid diagramId, SaveBusinessDiagramRequest request, CancellationToken ct = default)
    {
        await GetAccessAsync(analysisRunId, true, ct);
        await EnsureBusinessAsync(analysisRunId, businessId, ct);
        var diagram = await uow.RepoBusinesses.GetDiagramAsync(businessId, diagramId, true, ct)
            ?? throw new NotFoundException("diagram", diagramId);

        var title = RequireText(request.Title, "Tên diagram");
        if (await uow.RepoBusinesses.DiagramTitleExistsAsync(businessId, title, diagramId, ct))
            throw new ConflictException($"Nghiệp vụ đã có diagram tên \"{title}\".");

        diagram.Title = title;
        ApplyContent(diagram, request, businessId);
        diagram.UpdatedAt = DateTime.UtcNow;

        await uow.SaveChangesAsync(ct);
        return ToDetailDto(diagram);
    }

    public async Task DeleteDiagramAsync(Guid analysisRunId, Guid businessId, Guid diagramId, CancellationToken ct = default)
    {
        await GetAccessAsync(analysisRunId, true, ct);
        await EnsureBusinessAsync(analysisRunId, businessId, ct);
        var diagram = await uow.RepoBusinesses.GetDiagramAsync(businessId, diagramId, true, ct)
            ?? throw new NotFoundException("diagram", diagramId);

        uow.RepoBusinesses.RemoveDiagram(diagram);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<RepoAccess> GetAccessAsync(Guid analysisRunId, bool requireEdit, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated) throw new UnauthorizedException("Bạn cần đăng nhập.");

        var info = await uow.RepoBusinesses.GetAccessInfoAsync(analysisRunId, ct)
            ?? throw new NotFoundException("repo", analysisRunId);
        var edit = await CheckEditAsync(info, ct);

        if (requireEdit)
        {
            if (!edit.Allowed) throw new ForbiddenException(edit.Reason);
        }
        else if (!edit.Allowed && !await CanViewAsync(info, ct))
        {
            throw new ForbiddenException("Bạn không có quyền xem nghiệp vụ của repo này.");
        }

        return new RepoAccess(info, edit.Allowed);
    }

    private async Task<EditCheck> CheckEditAsync(RepoAccessInfo info, CancellationToken ct)
    {
        if (currentUser.Role != UserRole.Student)
            return new EditCheck(false, "Chỉ sinh viên trong nhóm thực hiện đề tài mới được chỉnh sửa nghiệp vụ.");
        if (info.IsArchived)
            return new EditCheck(false, "Không thể chỉnh sửa bản lịch sử của repo.");
        if (info.GroupId is not { } groupId)
            return new EditCheck(false, "Repo chưa được gắn với nhóm nào.");
        if (!await uow.RepoBusinesses.IsGroupMemberAsync(groupId, currentUser.UserId, ct))
            return new EditCheck(false, "Chỉ thành viên nhóm sở hữu repo mới được chỉnh sửa nghiệp vụ.");
        if (!await uow.RepoBusinesses.IsGroupTopicApprovedAsync(groupId, ct))
            return new EditCheck(false, "Nhóm chưa được duyệt vào đề tài nên chưa thể khai báo nghiệp vụ.");
        return new EditCheck(true, string.Empty);
    }

    private async Task<bool> CanViewAsync(RepoAccessInfo info, CancellationToken ct)
        => currentUser.Role switch
        {
            UserRole.Admin => true,
            UserRole.Lecturer => await uow.AnalysisRuns.IsLecturerOfRepoClassAsync(info.AnalysisRunId, currentUser.UserId, ct),
            UserRole.Student => await uow.AnalysisRuns.IsStudentOfRepoAsync(info.AnalysisRunId, currentUser.UserId, ct)
                || (info.GroupId is { } groupId
                    && await uow.RepoBusinesses.IsGroupMemberAsync(groupId, currentUser.UserId, ct)),
            _ => false
        };

    private async Task EnsureBusinessAsync(Guid analysisRunId, Guid businessId, CancellationToken ct)
    {
        if (!await uow.RepoBusinesses.BusinessExistsAsync(analysisRunId, businessId, ct))
            throw new NotFoundException("nghiệp vụ", businessId);
    }

    private async Task<string?> ResolveOwnerAsync(RepoAccessInfo info, string? requested, string? fallback, CancellationToken ct)
    {
        var code = Clean(requested);
        if (code is null) return fallback;
        if (info.GroupId is not { } groupId || !await uow.RepoBusinesses.IsStudentInGroupAsync(groupId, code, ct))
            throw new BadRequestException($"Sinh viên {code} không thuộc nhóm sở hữu repo.");
        return code;
    }

    private async Task<List<BusinessApi>> ResolveApisAsync(Guid analysisRunId, IEnumerable<Guid> methodSourceIds, CancellationToken ct)
    {
        var ids = methodSourceIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var methods = (await uow.RepoBusinesses.GetApiMethodsAsync(analysisRunId, ct)).ToDictionary(m => m.Id);
        var missing = ids.Where(id => !methods.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new BadRequestException($"Có API không tồn tại trong code hiện tại của repo, hãy tải lại danh sách API: {string.Join(", ", missing)}");

        return ids
            .Select(id => methods[id])
            .DistinctBy(m => (m.ClassName.ToLowerInvariant(), m.MethodName.ToLowerInvariant()))
            .Select((m, index) => new BusinessApi
            {
                Controller = Truncate(m.ClassName, 255)!,
                Function = Truncate(m.MethodName, 255)!,
                HttpMethod = Truncate(m.HttpVerb, 10),
                Path = Truncate(m.Route, 500),
                OrderIndex = index
            })
            .ToList();
    }

    private async Task<List<RepoBusinessDto>> BuildBusinessDtosAsync(Guid analysisRunId, CancellationToken ct)
    {
        var businesses = await uow.RepoBusinesses.GetBusinessesWithApisAsync(analysisRunId, ct);
        var diagrams = (await uow.RepoBusinesses.GetDiagramSummariesAsync(analysisRunId, ct)).ToLookup(d => d.BusinessId);
        var counts = await uow.RepoBusinesses.GetBusinessCountsAsync(analysisRunId, ct);
        var methods = await uow.RepoBusinesses.GetApiMethodsAsync(analysisRunId, ct);

        return businesses
            .Select(b => ToDto(b, diagrams[b.Id], counts.GetValueOrDefault(b.Id), methods))
            .ToList();
    }

    private async Task<RepoBusinessDto> GetBusinessDtoAsync(Guid analysisRunId, Guid businessId, CancellationToken ct)
        => (await BuildBusinessDtosAsync(analysisRunId, ct)).FirstOrDefault(b => b.Id == businessId)
            ?? throw new NotFoundException("nghiệp vụ", businessId);

    private static RepoBusinessDto ToDto(
        Business business,
        IEnumerable<BusinessDiagram> diagrams,
        BusinessCounts? counts,
        IReadOnlyList<RepoApiMethod> methods)
        => new(
            business.Id,
            business.AnalysisRunId,
            business.BusinessName,
            business.Description,
            business.OrderIndex,
            business.OwnerStudentCode,
            counts?.MethodCount ?? 0,
            counts?.FlowCount ?? 0,
            counts?.QuestionCount ?? 0,
            business.Apis
                .OrderBy(a => a.OrderIndex)
                .Select(a =>
                {
                    var method = methods.FirstOrDefault(m => Matches(a, m));
                    return new BusinessApiDto(a.Id, a.Controller, a.Function, a.HttpMethod, a.Path, a.OrderIndex,
                        method is not null, method?.Id);
                })
                .ToList(),
            diagrams.Select(ToSummaryDto).ToList(),
            business.UpdatedAt);

    private static BusinessDiagramSummaryDto ToSummaryDto(BusinessDiagram diagram)
    {
        var parsed = Deserialize(diagram.SpecificationJson);
        return new BusinessDiagramSummaryDto(
            diagram.Id,
            diagram.BusinessId,
            diagram.Title,
            diagram.Format,
            diagram.PreviewUrl,
            diagram.Description,
            parsed?.Nodes.Count ?? 0,
            parsed?.Edges.Count ?? 0,
            diagram.UpdatedAt);
    }

    private static BusinessDiagramDto ToDetailDto(BusinessDiagram diagram)
        => new(ToSummaryDto(diagram), diagram.SourceXml, Deserialize(diagram.SpecificationJson)?.NormalizedFlowText);

    private static void ApplyContent(BusinessDiagram diagram, SaveBusinessDiagramRequest request, Guid businessId)
    {
        if (request.Format != DiagramFormat.Drawio)
            throw new BadRequestException("Chỉ nhận sơ đồ draw.io (.drawio hoặc .xml) để AI đọc được nội dung khi sinh câu hỏi.");

        var xml = request.SourceXml?.Trim();
        if (string.IsNullOrEmpty(xml))
            throw new BadRequestException("Diagram draw.io cần có nội dung XML.");
        if (xml.Length > MaxSourceXmlLength)
            throw new BadRequestException("File draw.io quá lớn, tối đa 2 MB.");

        DrawioProcessDiagram parsed;
        try
        {
            parsed = DrawioXmlParser.Parse(xml, businessId, diagram.Title);
        }
        catch (Exception ex) when (ex is ArgumentException or XmlException or InvalidOperationException
            or FormatException or InvalidDataException)
        {
            throw new BadRequestException($"Không đọc được XML draw.io: {ex.Message}", ex);
        }

        diagram.Format = DiagramFormat.Drawio;
        diagram.PreviewUrl = null;
        diagram.Description = Clean(request.Description);
        diagram.SourceXml = xml;
        diagram.SpecificationJson = JsonSerializer.Serialize(parsed, JsonOpts);
    }

    private static DrawioProcessDiagram? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<DrawioProcessDiagram>(json, JsonOpts); }
        catch (JsonException) { return null; }
    }

    private static bool Matches(BusinessApi api, RepoApiMethod method)
        => string.Equals(api.Controller, method.ClassName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(api.Function, method.MethodName, StringComparison.OrdinalIgnoreCase);

    private static (string, string) Key(BusinessApi api)
        => (api.Controller.ToLowerInvariant(), api.Function.ToLowerInvariant());

    private static string RequireText(string? value, string field)
        => Clean(value) ?? throw new BadRequestException($"{field} không được để trống.");

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max];
}
