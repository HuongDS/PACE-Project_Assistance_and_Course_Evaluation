using PACE.Application.Common.ReadModels;

namespace PACE.Application.Oral;

/// <summary>Một hàm trong mô hình nghiệp vụ dựng tại mốc hạn; <paramref name="Key"/> trùng khóa của bản ghi ai viết dòng nào.</summary>
public record ContextMethod(Guid Id, string Key, string ClassName, string MethodName, string SourceCode);

public record ContextBusiness(string Name, List<FlowQuestionContext> Flows, List<Guid> MethodIds);

/// <summary>Nghiệp vụ, luồng gọi và sơ đồ của repo tại mốc hạn. Chỉ dùng cho vấn đáp, không ghi vào dữ liệu phân tích.</summary>
public record ResearchContext(List<ContextBusiness> Businesses, List<ContextMethod> Methods);

/// <summary>
/// Phần việc của một sinh viên trong một nghiệp vụ: các luồng bạn ấy có viết mã, đúng như đơn vị đưa vào prompt của nghiên cứu.
/// <paramref name="Owned"/> là các hàm trong đó do bạn ấy viết.
/// </summary>
public record StudentUnit(string Business, List<MethodCode> Methods, List<FlowQuestionContext> Flows, List<OwnedMethod> Owned, string? GroundTruth = null)
{
    public int OwnLines => Owned.Sum(o => o.OwnLines.Count);
}

public static class StudentWorkload
{
    public const string OutsideFlows = "Phần mã ngoài luồng nghiệp vụ";
    public const int MaxOutsideMethods = 8;

    /// <summary>Các nghiệp vụ sinh viên có tham gia, nghiệp vụ viết nhiều đứng trước.</summary>
    public static List<StudentUnit> For(ResearchContext context, CodeSnapshot snapshot, Guid studentId)
    {
        var owned = OralOwnership.OwnedBy(snapshot, studentId).ToDictionary(o => o.Method.Key, StringComparer.Ordinal);
        if (owned.Count == 0) return [];
        var methods = context.Methods.ToDictionary(m => m.Id);

        var units = new List<StudentUnit>();
        foreach (var business in context.Businesses)
        {
            bool Mine(Guid id) => methods.TryGetValue(id, out var m) && owned.ContainsKey(m.Key);
            static IEnumerable<Guid> Steps(FlowQuestionContext flow)
                => flow.Edges.SelectMany(e => new[] { e.FromMethodId, e.ToMethodId }).Prepend(flow.EntryMethodId);

            var flows = business.Flows.Where(f => Steps(f).Any(Mine)).OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
            var inFlows = business.Flows.SelectMany(Steps).ToHashSet();
            // Màn giao diện gắn với nghiệp vụ nhưng không nằm trong luồng nào cũng tính nếu bạn ấy viết.
            var ids = flows.SelectMany(Steps).Concat(business.MethodIds.Where(id => !inFlows.Contains(id) && Mine(id))).ToHashSet();
            var mine = ids.Where(Mine).Select(id => owned[methods[id].Key]).ToList();
            if (mine.Count == 0) continue;

            units.Add(new StudentUnit(business.Name,
                ids.Where(methods.ContainsKey).Select(id => methods[id])
                    .OrderBy(m => m.ClassName, StringComparer.Ordinal).ThenBy(m => m.MethodName, StringComparer.Ordinal)
                    .Select(m => new MethodCode(m.Id, m.ClassName, m.MethodName, m.SourceCode)).ToList(),
                flows, mine));
        }
        if (units.Count > 0) return units.OrderByDescending(u => u.OwnLines).ThenBy(u => u.Business, StringComparer.Ordinal).ToList();

        // Không có mã nào nằm trong luồng nghiệp vụ: chỉ còn cách đưa mã thô của bạn ấy.
        var outside = owned.Values.OrderByDescending(o => o.Score).Take(MaxOutsideMethods).ToList();
        return
        [
            new StudentUnit(OutsideFlows, outside.Select(o =>
            {
                var dot = o.Method.Name.LastIndexOf('.');
                return new MethodCode(Guid.NewGuid(), dot > 0 ? o.Method.Name[..dot] : "", dot > 0 ? o.Method.Name[(dot + 1)..] : o.Method.Name, o.Method.Code);
            }).ToList(), [], outside)
        ];
    }
}
