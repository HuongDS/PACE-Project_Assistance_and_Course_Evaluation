using System.IO.Compression;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PACE.Application.Analysis;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Enums;
using PACE.Infrastructure.Excel;
using Xunit;

namespace PACE.Tests;

public class BatchTemplateTests
{
    private static MemoryStream Workbook(Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook();
        fill(workbook.AddWorksheet("Sheet1"));
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // ---------- đọc Excel ----------

    [Fact]
    public void Reader_finds_repo_column_by_header_and_skips_blank_rows()
    {
        using var file = Workbook(s =>
        {
            s.Cell(1, 1).Value = "Nhóm"; s.Cell(1, 2).Value = "Link GitHub";
            s.Cell(2, 1).Value = "G1"; s.Cell(2, 2).Value = "https://github.com/a/one";
            s.Cell(3, 1).Value = "G2";
            s.Cell(4, 2).Value = "https://github.com/a/two";
        });

        var rows = new ClosedXmlRepoLinkReader().Read(file);

        Assert.Equal([(2, "https://github.com/a/one"), (4, "https://github.com/a/two")], rows.Select(r => (r.Row, r.RepoUrl)));
    }

    [Fact]
    public void Reader_accepts_a_plain_list_of_links_without_header()
    {
        using var file = Workbook(s =>
        {
            s.Cell(1, 1).Value = "https://github.com/a/one";
            s.Cell(2, 1).Value = "https://github.com/a/two";
        });

        var rows = new ClosedXmlRepoLinkReader().Read(file);

        Assert.Equal([1, 2], rows.Select(r => r.Row));
    }

    [Fact]
    public void Reader_uses_hyperlink_address_when_cell_shows_a_label()
    {
        using var file = Workbook(s =>
        {
            s.Cell(1, 1).Value = "Repo";
            s.Cell(2, 1).Value = "Backend";
            s.Cell(2, 1).SetHyperlink(new XLHyperlink("https://github.com/a/backend"));
        });

        var row = Assert.Single(new ClosedXmlRepoLinkReader().Read(file));

        Assert.Equal("https://github.com/a/backend", row.RepoUrl.TrimEnd('/'));
    }

    [Fact]
    public void Reader_rejects_files_without_a_repo_column_or_links()
    {
        using var file = Workbook(s => { s.Cell(1, 1).Value = "Họ tên"; s.Cell(2, 1).Value = "An"; });

        Assert.Throws<BadRequestException>(() => new ClosedXmlRepoLinkReader().Read(file));
        Assert.Throws<BadRequestException>(() => new ClosedXmlRepoLinkReader().Read(new MemoryStream([1, 2, 3])));
    }

    // ---------- xử lý hàng loạt ----------

    private sealed class FakeReader(params RepoLinkRow[] rows) : IRepoLinkReader
    {
        public IReadOnlyList<RepoLinkRow> Read(Stream excelStream) => rows;
    }

    private sealed class FakeScopeFactory(ITemplateGenerationService service) : IServiceScopeFactory
    {
        public int ScopesCreated;
        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref ScopesCreated);
            return new Scope(service);
        }

        private sealed class Scope(ITemplateGenerationService service) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } =
                Stub<IServiceProvider>.Create((m, a) => (Type)a[0]! == typeof(ITemplateGenerationService) ? service : null);
            public void Dispose() { }
        }
    }

    private static GeneratedTemplateDto Template(string url)
        => new(url, BusinessTemplateSource.Controller, ".NET", 1, 1,
            [new BusinessTemplate { BusinessName = "Ngân sách", Apis = [new ApiTemplate { Controller = "BudgetController", HttpMethod = "GET", Function = "List", Path = "/api/budget" }] }],
            []);

    private static (BatchTemplateService Service, FakeScopeFactory Scopes, List<string> Calls) Batch(
        Func<GenerateTemplateRequest, Task<GeneratedTemplateDto>> generate, params RepoLinkRow[] rows)
    {
        var calls = new List<string>();
        var service = Stub<ITemplateGenerationService>.Create((m, a) =>
        {
            var request = (GenerateTemplateRequest)a[0]!;
            lock (calls) calls.Add($"{request.RepoUrl}|ai={request.UseAi}|regen={request.Regenerate}");
            return generate(request);
        });
        var scopes = new FakeScopeFactory(service);
        return (new BatchTemplateService(new FakeReader(rows), scopes, NullLogger<BatchTemplateService>.Instance), scopes, calls);
    }

    [Fact]
    public async Task Batch_isolates_failures_flags_invalid_and_duplicate_links_and_keeps_row_order()
    {
        var (service, scopes, calls) = Batch(request => request.RepoUrl.EndsWith("/bad")
                ? throw new UnprocessableException("Không tìm thấy endpoint.")
                : request.RepoUrl.EndsWith("/boom")
                    ? throw new InvalidOperationException("secret detail")
                    : Task.FromResult(Template(request.RepoUrl)),
            new RepoLinkRow(2, "https://github.com/a/ok"),
            new RepoLinkRow(3, "https://github.com/a/bad"),
            new RepoLinkRow(4, "ftp-not-a-link"),
            new RepoLinkRow(5, "https://github.com/A/OK.git"),
            new RepoLinkRow(6, "https://github.com/a/boom"));

        var result = await service.GenerateAsync(Stream.Null, useAi: false, regenerate: true);

        Assert.Equal([2, 3, 4, 5, 6], result.Items.Select(i => i.Row));
        Assert.Equal([BatchItemStatus.Succeeded, BatchItemStatus.Failed, BatchItemStatus.Failed, BatchItemStatus.Skipped, BatchItemStatus.Failed],
            result.Items.Select(i => i.Status));
        Assert.Equal((5, 1, 3, 1), (result.Total, result.Succeeded, result.Failed, result.Skipped));
        Assert.Equal("Không tìm thấy endpoint.", result.Items[1].Error);
        Assert.Equal("Trùng với dòng 2.", result.Items[3].Error);
        Assert.DoesNotContain("secret", result.Items[4].Error);
        Assert.NotNull(result.Items[0].Template);

        Assert.Equal(3, scopes.ScopesCreated);
        Assert.All(calls, c => Assert.EndsWith("|ai=False|regen=True", c));
    }

    [Fact]
    public async Task Batch_rejects_empty_and_oversized_files_before_cloning_anything()
    {
        var (empty, _, _) = Batch(r => Task.FromResult(Template(r.RepoUrl)));
        await Assert.ThrowsAsync<BadRequestException>(() => empty.GenerateAsync(Stream.Null, true, false));

        var rows = Enumerable.Range(1, BatchTemplateService.MaxRepos + 1).Select(i => new RepoLinkRow(i, $"https://github.com/a/r{i}")).ToArray();
        var (big, _, calls) = Batch(r => Task.FromResult(Template(r.RepoUrl)), rows);
        await Assert.ThrowsAsync<BadRequestException>(() => big.GenerateAsync(Stream.Null, true, false));
        Assert.Empty(calls);
    }

    [Fact]
    public void Error_summary_is_one_line_and_hides_the_server_temp_path()
    {
        const string git = "[git] Clone thất bại: Cloning into 'C:\\Users\\x\\Temp\\pace_repos\\clone_1'...\nremote: Repository not found.\nfatal: repository 'https://github.com/a/b.git/' not found. Nếu repo private, cấu hình Analysis:GitHubToken.";

        var summary = BatchTemplateService.Summarize(git);

        Assert.DoesNotContain('\n', summary);
        Assert.DoesNotContain("pace_repos", summary);
        Assert.StartsWith("[git] Clone thất bại.", summary);
        Assert.Contains("Analysis:GitHubToken", summary);
        Assert.Equal(503, BatchTemplateService.Summarize(new string('x', 2000)).Length);
    }

    // ---------- file zip ----------

    [Fact]
    public void Archive_contains_one_folder_per_successful_repo_and_a_report()
    {
        var result = new BatchTemplateResultDto(4, 2, 1, 1,
        [
            new(2, "https://github.com/a/one", BatchItemStatus.Succeeded, null, Template("https://github.com/a/one")),
            new(3, "https://github.com/a/two", BatchItemStatus.Failed, "Repo private.", null),
            new(4, "https://gitlab.com/a/one", BatchItemStatus.Succeeded, null, Template("https://gitlab.com/a/one")),
            new(5, "https://github.com/a/one", BatchItemStatus.Skipped, "Trùng với dòng 2.", null)
        ]);

        using var zip = new ZipArchive(new MemoryStream(TemplateArchiveBuilder.Build(result)));
        var names = zip.Entries.Select(e => e.FullName).ToList();

        Assert.Equal(["_report.txt", "a_one/template_business.json", "a_one_2/template_business.json"], names.Order(StringComparer.Ordinal));

        using var json = new StreamReader(zip.GetEntry("a_one/template_business.json")!.Open());
        var loaded = BusinessTemplateLoader.Parse(json.ReadToEnd());
        Assert.Equal("Ngân sách", Assert.Single(loaded).BusinessName);

        using var report = new StreamReader(zip.GetEntry("_report.txt")!.Open());
        var text = report.ReadToEnd();
        Assert.Contains("Dòng 3: FAILED", text);
        Assert.Contains("Repo private.", text);
        Assert.Contains("Dòng 5: SKIPPED", text);
    }
}
