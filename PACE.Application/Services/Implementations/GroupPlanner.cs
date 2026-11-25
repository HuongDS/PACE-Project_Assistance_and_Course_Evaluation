namespace PACE.Application.Services.Implementations;

public sealed record PlanInputGroup(Guid Id, string Name, IReadOnlyList<string> Members);

public sealed record PlanGroupResult(Guid Id, string Name, IReadOnlyList<string> Members);

/// <param name="Groups">Mọi nhóm của lớp với danh sách thành viên đề xuất; nhóm rỗng sẽ bị bỏ khi áp dụng.</param>
/// <param name="Unplaced">Sinh viên không còn chỗ trong bất kỳ nhóm nào.</param>
public sealed record GroupPlan(IReadOnlyList<PlanGroupResult> Groups, IReadOnlyList<string> Unplaced);

/// <summary>Quy tắc chia nhóm: số nhóm cần mở và cách xếp sinh viên còn lại khi hết hạn đăng ký.</summary>
public static class GroupPlanner
{
    /// <summary>Số nhóm nhiều nhất mà mỗi nhóm vẫn có thể đủ số thành viên tối thiểu.</summary>
    public static int GroupCount(int students, int minMembers) =>
        students <= 0 ? 0 : Math.Max(1, students / Math.Max(1, minMembers));

    /// <summary>
    /// Đề xuất xếp nhóm, giữ lựa chọn của sinh viên nhiều nhất có thể:
    /// 1. bù người cho các nhóm còn thiếu, nhóm gần đủ trước;
    /// 2. nếu còn đủ người, lập nhóm mới trong các nhóm đang trống;
    /// 3. rải người còn lại vào nhóm ít người nhất còn chỗ;
    /// 4. giải tán nhóm vẫn thiếu người nếu các nhóm khác còn đủ chỗ nhận.
    /// </summary>
    public static GroupPlan Plan(IReadOnlyList<PlanInputGroup> groups, IReadOnlyList<string> unassigned, int minMembers, int maxMembers)
    {
        var result = groups
            .OrderBy(g => g.Name.Length).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Id, g.Name, Members: g.Members.ToList()))
            .ToList();
        var waiting = new Queue<string>(unassigned.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        foreach (var group in result.Where(g => g.Members.Count > 0 && g.Members.Count < minMembers).OrderByDescending(g => g.Members.Count))
            while (group.Members.Count < minMembers && waiting.Count > 0)
                group.Members.Add(waiting.Dequeue());

        foreach (var group in result.Where(g => g.Members.Count == 0))
        {
            if (waiting.Count < minMembers) break;
            while (group.Members.Count < minMembers) group.Members.Add(waiting.Dequeue());
        }

        var unplaced = new List<string>();
        while (waiting.Count > 0)
        {
            var student = waiting.Dequeue();
            var target = result.Where(g => g.Members.Count > 0 && g.Members.Count < maxMembers).OrderBy(g => g.Members.Count).Select(g => g.Members).FirstOrDefault()
                         ?? result.Where(g => g.Members.Count == 0).Select(g => g.Members).FirstOrDefault();
            if (target is null) unplaced.Add(student);
            else target.Add(student);
        }

        while (true)
        {
            var weak = result
                .Where(g => g.Members.Count > 0 && g.Members.Count < minMembers)
                .OrderBy(g => g.Members.Count)
                .FirstOrDefault(g => result.Where(o => o.Id != g.Id && o.Members.Count > 0).Sum(o => maxMembers - o.Members.Count) >= g.Members.Count);
            if (weak.Members is null) break;

            foreach (var student in weak.Members)
            {
                var target = result
                    .Where(o => o.Id != weak.Id && o.Members.Count > 0 && o.Members.Count < maxMembers)
                    // Ưu tiên làm đầy nhóm cũng đang thiếu, sau đó tới nhóm ít người nhất.
                    .OrderByDescending(o => o.Members.Count < minMembers)
                    .ThenBy(o => o.Members.Count < minMembers ? -o.Members.Count : o.Members.Count)
                    .First();
                target.Members.Add(student);
            }
            weak.Members.Clear();
        }

        return new GroupPlan(result.Select(g => new PlanGroupResult(g.Id, g.Name, g.Members)).ToList(), unplaced);
    }
}
