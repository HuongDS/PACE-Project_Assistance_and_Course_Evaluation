using PACE.Infrastructure.Repositories;
using Xunit;

namespace PACE.Tests;

/// <summary>Một sinh viên bị ghi vào hai nhóm của cùng lớp không được làm hỏng trang lớp.</summary>
public class DuplicateMembershipTests
{
    [Fact]
    public void Student_In_Two_Groups_Shows_Both_Names()
    {
        var names = ClassRepository.GroupNames(
        [
            ("SE170006", "RoomFinder"),
            ("SE170006", "Mobile Devs"),
            ("SE170007", "RoomFinder"),
        ]);

        Assert.Equal("Mobile Devs, RoomFinder", names["SE170006"]);
        Assert.Equal("RoomFinder", names["SE170007"]);
    }
}
