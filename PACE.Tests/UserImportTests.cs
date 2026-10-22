using ClosedXML.Excel;
using PACE.Application.Common.Interfaces;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Excel;
using Xunit;

namespace PACE.Tests;

public class UserImportTests
{
    private const string Repo = "https://github.com/team/project";

    [Fact]
    public void ReaderTakesStudentCodeColumnAndKeepsGroupName()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Import");
        sheet.Cell(1, 1).Value = "Email"; sheet.Cell(1, 2).Value = "MSSV"; sheet.Cell(1, 3).Value = "Tên nhóm";
        sheet.Cell(1, 4).Value = "Họ tên SV"; sheet.Cell(1, 5).Value = "Link GitHub";
        sheet.Cell(2, 1).Value = "baonvse183398@fpt.edu.vn"; sheet.Cell(2, 2).Value = " se183398 "; sheet.Cell(2, 3).Value = "Nhóm 1";
        sheet.Cell(2, 4).Value = "Nguyễn Viết Bảo"; sheet.Cell(2, 5).Value = Repo;
        sheet.Cell(3, 1).Value = "nocode@fpt.edu.vn"; sheet.Cell(3, 5).Value = Repo;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var rows = new ClosedXmlUserImportReader().Read(stream);

        Assert.Equal(2, rows.Count);
        Assert.Equal("se183398", rows[0].StudentCode);
        Assert.Equal("Nhóm 1", rows[0].RepoName);
        Assert.Null(rows[1].StudentCode);
    }

    [Fact]
    public async Task NewStudentsGetTheirStudentCode()
    {
        var env = new Env([new UserImportRow(2, "new@fpt.edu.vn", Repo, "Nhóm 1", "se183398")]);

        var result = await env.Service.ImportFromExcelAsync(new MemoryStream());

        Assert.Equal(1, result.CreatedUsers);
        var user = Assert.Single(env.Added);
        Assert.Equal("SE183398", user.StudentCode);
        Assert.Equal(UserRole.Student, user.Role);
    }

    [Fact]
    public async Task ExistingStudentWithoutCodeIsFilledIn()
    {
        var existing = new User { Id = Guid.NewGuid(), Email = "old@fpt.edu.vn", Role = UserRole.Student, AnalysisRunId = Guid.NewGuid() };
        var env = new Env([new UserImportRow(2, "old@fpt.edu.vn", Repo, null, "SE180768")], existing);

        var result = await env.Service.ImportFromExcelAsync(new MemoryStream());

        Assert.Equal(1, result.UpdatedUsers);
        Assert.Equal(0, result.CreatedUsers);
        Assert.Empty(result.Skipped);
        Assert.Equal("SE180768", Assert.Single(env.Updated).StudentCode);
    }

    [Fact]
    public async Task ExistingStudentWithCodeIsLeftUnchanged()
    {
        var existing = new User { Id = Guid.NewGuid(), Email = "old@fpt.edu.vn", StudentCode = "SE111111", Role = UserRole.Student };
        var env = new Env([new UserImportRow(2, "old@fpt.edu.vn", Repo, null, "SE180768")], existing);

        var result = await env.Service.ImportFromExcelAsync(new MemoryStream());

        Assert.Equal(0, result.UpdatedUsers);
        Assert.Empty(env.Updated);
        Assert.Equal("Email đã tồn tại.", Assert.Single(result.Skipped).Reason);
    }

    [Fact]
    public async Task CodeOwnedByAnotherAccountOrInvalidIsSkipped()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = "owner@fpt.edu.vn", StudentCode = "SE222222", Role = UserRole.Student };
        var env = new Env([
            new UserImportRow(2, "a@fpt.edu.vn", Repo, null, "SE222222"),
            new UserImportRow(3, "b@fpt.edu.vn", Repo, null, "not a code"),
            new UserImportRow(4, "c@fpt.edu.vn", Repo, null, "SE333333"),
            new UserImportRow(5, "d@fpt.edu.vn", Repo, null, "se333333"),
        ], owner);

        var result = await env.Service.ImportFromExcelAsync(new MemoryStream());

        Assert.Equal(1, result.CreatedUsers);
        Assert.Equal("SE333333", Assert.Single(env.Added).StudentCode);
        Assert.Equal([2, 3, 5], result.Skipped.Select(s => s.Row));
    }

    private sealed class Env
    {
        public List<User> Added { get; } = [];
        public List<User> Updated { get; } = [];
        public UserService Service { get; }

        public Env(IReadOnlyList<UserImportRow> rows, params User[] existing)
        {
            var users = Stub<IUserRepository>.Create((m, a) => m.Name switch
            {
                "GetAllEmailsAsync" => Task.FromResult(existing.Select(u => u.Email).ToHashSet()),
                "GetByEmailAsync" => Task.FromResult(existing.FirstOrDefault(u => u.Email == (string)a[0]!)),
                "GetByStudentCodeAsync" => Task.FromResult(existing.FirstOrDefault(u => u.StudentCode == (string)a[0]!)),
                "AddRangeAsync" => AddAll((IEnumerable<User>)a[0]!),
                "Update" => Track((User)a[0]!),
                _ => throw new Exception($"Unexpected user operation {m.Name}"),
            });
            var runs = Stub<IAnalysisRunRepository>.Create((m, _) => m.Name switch
            {
                "GetAllWithRepoUrlAsync" => Task.FromResult(new List<AnalysisRun>()),
                "AddRangeAsync" => Task.CompletedTask,
                _ => throw new Exception($"Unexpected repo operation {m.Name}"),
            });
            var uow = Stub<IUnitOfWork>.Create((m, a) => m.Name switch
            {
                "get_Users" => users,
                "get_AnalysisRuns" => runs,
                "ExecuteInTransactionAsync" => ((Func<CancellationToken, Task>)a[0]!)(CancellationToken.None),
                _ => throw new Exception($"Unexpected unit-of-work operation {m.Name}"),
            });
            var reader = Stub<IUserImportReader>.Create((_, _) => rows);
            Service = new UserService(uow, Stub<ICurrentUserService>.Create((m, _) => throw new NotSupportedException(m.Name)), reader);
        }

        private Task AddAll(IEnumerable<User> users) { Added.AddRange(users); return Task.CompletedTask; }
        private object? Track(User user) { Updated.Add(user); return null; }
    }
}
