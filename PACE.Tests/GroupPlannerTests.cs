using PACE.Application.Services.Implementations;
using Xunit;

namespace PACE.Tests;

public class GroupPlannerTests
{
    private static PlanInputGroup Group(string name, params string[] members) => new(Guid.NewGuid(), name, members);

    private static string[] Students(int count, int from = 1) => Enumerable.Range(from, count).Select(i => $"SE{i:000}").ToArray();

    private static List<int> Sizes(GroupPlan plan) => plan.Groups.Where(g => g.Members.Count > 0).Select(g => g.Members.Count).OrderByDescending(x => x).ToList();

    private static void AssertEveryoneOnce(GroupPlan plan, IEnumerable<string> everyone) =>
        Assert.Equal(everyone.OrderBy(x => x), plan.Groups.SelectMany(g => g.Members).Concat(plan.Unplaced).OrderBy(x => x));

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(2, 3, 1)]
    [InlineData(23, 4, 5)]
    [InlineData(24, 4, 6)]
    public void GroupCount_IsTheMostGroupsThatCanReachTheMinimum(int students, int min, int expected) =>
        Assert.Equal(expected, GroupPlanner.GroupCount(students, min));

    [Fact]
    public void EmptyClassGroups_AreFilledEvenly()
    {
        var groups = Enumerable.Range(1, 5).Select(i => Group($"Nhóm {i}")).ToList();

        var plan = GroupPlanner.Plan(groups, Students(23), 4, 5);

        Assert.Equal([5, 5, 5, 4, 4], Sizes(plan));
        Assert.Empty(plan.Unplaced);
        AssertEveryoneOnce(plan, Students(23));
    }

    [Fact]
    public void UnderfilledGroups_ReceiveWaitingStudentsFirst_AndKeepTheirMembers()
    {
        var almost = Group("Nhóm 1", "SE101", "SE102");
        var full = Group("Nhóm 2", "SE103", "SE104", "SE105");

        var plan = GroupPlanner.Plan([almost, full], ["SE001"], 3, 5);

        Assert.Equal(["SE101", "SE102", "SE001"], plan.Groups.Single(g => g.Id == almost.Id).Members);
        Assert.Equal(3, plan.Groups.Single(g => g.Id == full.Id).Members.Count);
    }

    [Fact]
    public void GroupThatCannotReachMinimum_IsDissolvedIntoOthers()
    {
        var strong = Group("Nhóm 1", "SE101", "SE102", "SE103");
        var lonely = Group("Nhóm 2", "SE104");

        var plan = GroupPlanner.Plan([strong, lonely], [], 3, 5);

        Assert.Empty(plan.Groups.Single(g => g.Id == lonely.Id).Members);
        Assert.Equal(4, plan.Groups.Single(g => g.Id == strong.Id).Members.Count);
    }

    [Fact]
    public void TwoSmallGroups_MergeIntoOne()
    {
        var plan = GroupPlanner.Plan([Group("Nhóm 1", "SE101", "SE102"), Group("Nhóm 2", "SE103", "SE104")], [], 3, 5);

        Assert.Equal([4], Sizes(plan));
    }

    [Fact]
    public void SmallGroup_StaysWhenNobodyHasRoom()
    {
        var plan = GroupPlanner.Plan([Group("Nhóm 1", "SE101", "SE102", "SE103"), Group("Nhóm 2", "SE104", "SE105")], [], 3, 3);

        Assert.Equal([3, 2], Sizes(plan));
    }

    [Fact]
    public void StudentsBeyondCapacity_AreReportedAsUnplaced()
    {
        var plan = GroupPlanner.Plan([Group("Nhóm 1")], Students(5), 2, 3);

        Assert.Equal([3], Sizes(plan));
        Assert.Equal(2, plan.Unplaced.Count);
        AssertEveryoneOnce(plan, Students(5));
    }

    [Fact]
    public void EveryRandomShape_RespectsMaximumAndKeepsEveryone()
    {
        var random = new Random(7);
        for (var round = 0; round < 500; round++)
        {
            var max = random.Next(2, 7);
            var min = random.Next(1, max + 1);
            var next = 1;
            var groups = Enumerable.Range(1, random.Next(1, 8)).Select(i =>
            {
                var members = Students(random.Next(0, max + 1), next);
                next += members.Length;
                return Group($"Nhóm {i}", members);
            }).ToList();
            var waiting = Students(random.Next(0, 15), next);

            var plan = GroupPlanner.Plan(groups, waiting, min, max);

            Assert.All(plan.Groups, g => Assert.True(g.Members.Count <= max));
            AssertEveryoneOnce(plan, groups.SelectMany(g => g.Members).Concat(waiting));
        }
    }
}
