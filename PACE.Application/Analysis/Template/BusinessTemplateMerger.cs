namespace PACE.Application.Analysis.Template;

/// <summary>
/// Gộp template vừa tạo lại từ code mới vào danh sách nghiệp vụ nhóm đang có, không xoá gì:
/// nghiệp vụ cũ giữ nguyên (tên, mô tả, API đã gán, sơ đồ, câu hỏi); API mới chưa thuộc nghiệp vụ nào
/// được thêm vào nghiệp vụ trùng tên, hoặc tạo nghiệp vụ mới nếu chưa có nghiệp vụ trùng tên.
/// </summary>
public static class BusinessTemplateMerger
{
    public sealed record Result(
        List<BusinessTemplate> Templates,
        HashSet<Guid> ChangedBusinessIds,
        int AddedBusinessCount,
        int AddedApiCount,
        List<string> Notes);

    /// <param name="current">Nghiệp vụ hiện có trong DB (dựng bằng <see cref="DeclaredBusinessTemplate"/>, có BusinessId).</param>
    /// <param name="generated">Template mới tạo lại từ code.</param>
    public static Result Merge(IReadOnlyList<BusinessTemplate> current, IReadOnlyList<BusinessTemplate> generated)
    {
        var templates = current.Select(Clone).ToList();
        var byName = new Dictionary<string, BusinessTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in templates) byName.TryAdd(t.BusinessName.Trim(), t);

        // Một API (controller + hàm) chỉ thuộc một nghiệp vụ: API nhóm đã gán ở đâu thì để nguyên ở đó.
        var assigned = new HashSet<string>(templates.SelectMany(t => t.Apis).Select(ApiKey));
        var changed = new HashSet<Guid>();
        var notes = new List<string>();
        var addedBusinesses = 0;
        var addedApis = 0;

        foreach (var gen in generated)
        {
            var name = gen.BusinessName?.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var newApis = (gen.Apis ?? [])
                .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Controller) && a.ResolveFunctionName().Length > 0)
                .Where(a => assigned.Add(ApiKey(a)))
                .ToList();
            if (newApis.Count == 0) continue;
            addedApis += newApis.Count;

            if (byName.TryGetValue(name, out var target))
            {
                target.Apis.AddRange(newApis);
                if (target.BusinessId is { } id) changed.Add(id);
                notes.Add($"[{target.BusinessName}] Thêm {newApis.Count} API mới từ code: {string.Join(", ", newApis.Select(a => a.DisplayName))}.");
                continue;
            }

            var created = new BusinessTemplate
            {
                BusinessName = name,
                Description = gen.Description,
                Apis = newApis
            };
            templates.Add(created);
            byName[name] = created;
            addedBusinesses++;
            notes.Add($"[{name}] Nghiệp vụ mới tạo từ code ({newApis.Count} API), nhóm có thể đổi tên hoặc gộp lại trên web.");
        }

        return new Result(templates, changed, addedBusinesses, addedApis, notes);
    }

    internal static string ApiKey(ApiTemplate api)
        => $"{api.Controller.Trim().ToLowerInvariant()}|{api.ResolveFunctionName().ToLowerInvariant()}";

    private static BusinessTemplate Clone(BusinessTemplate t) => new()
    {
        BusinessId = t.BusinessId,
        BusinessName = t.BusinessName,
        Description = t.Description,
        Apis = [.. t.Apis]
    };
}
