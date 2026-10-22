using Microsoft.Extensions.Logging.Abstractions;
using PACE.Application.Analysis;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Common;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using Xunit;

namespace PACE.Tests;

public class RepoUrlHelperTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("https://github.com/owner/repo/")]
    [InlineData("https://github.com/owner/repo/tree/main/backend")]
    [InlineData("https://www.github.com/owner/repo")]
    [InlineData("https://gitlab.com/group/project")]
    [InlineData("https://bitbucket.org/team/repo")]
    [InlineData("https://git.example.edu.vn/anything")]
    public void Valid_repo_links_have_no_problem(string url)
        => Assert.Null(RepoUrlHelper.FindProblem(url));

    [Theory]
    [InlineData("https://github.com/WPD301-Sports-Center-Management-System", "WPD301-Sports-Center-Management-System")]
    [InlineData("https://github.com/WPD301-Sports-Center-Management-System/", "WPD301-Sports-Center-Management-System")]
    [InlineData("https://github.com/someone?tab=repositories", "someone")]
    [InlineData("https://www.github.com/someone", "someone")]
    [InlineData("https://gitlab.com/some-group", "some-group")]
    public void Link_with_only_a_user_or_organization_says_the_repo_name_is_missing(string url, string owner)
    {
        var problem = RepoUrlHelper.FindProblem(url);

        Assert.NotNull(problem);
        Assert.Contains("thiếu tên repo", problem);
        Assert.Contains(owner, problem);
        Assert.DoesNotContain("clone", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://github.com")]
    [InlineData("https://github.com/")]
    public void Link_to_the_host_only_asks_for_user_and_repo(string url)
        => Assert.Contains("tên người dùng/tổ chức và tên repo", RepoUrlHelper.FindProblem(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("khong phai link")]
    [InlineData("github.com/owner/repo")]
    [InlineData("ftp://github.com/owner/repo")]
    public void Anything_that_is_not_an_http_link_is_invalid(string? url)
        => Assert.Equal("Link repo không hợp lệ.", RepoUrlHelper.FindProblem(url));

    [Theory]
    [InlineData("https://github.com/owner/repo.git/tree/Feat/FixUnhappyCase/", "https://github.com/owner/repo/tree/Feat/FixUnhappyCase")]
    [InlineData("https://www.github.com/owner/repo/tree/dev?tab=readme", "https://github.com/owner/repo/tree/dev")]
    [InlineData("https://gitlab.com/team/repo/-/tree/feature/api/backend", "https://gitlab.com/team/repo/-/tree/feature/api/backend")]
    [InlineData("https://bitbucket.org/team/repo/src/dev", "https://bitbucket.org/team/repo/src/dev")]
    [InlineData("https://github.com/owner/repo/tree/Feat%2FFixUnhappyCase", "https://github.com/owner/repo/tree/Feat/FixUnhappyCase")]
    public void NormalizationKeepsBranchAndFolder(string input, string expected)
        => Assert.Equal(expected, RepoUrlHelper.Normalize(input));

    [Fact] public void SourceIdentityPreservesBranchCaseAndSeparatesDefaultBranch()
    {
        var main = RepoUrlHelper.SourceKey("https://github.com/Owner/Repo");
        var branch = RepoUrlHelper.SourceKey("https://github.com/OWNER/REPO/tree/Feat/Fix");
        Assert.NotEqual(main, branch);
        Assert.NotEqual(branch, RepoUrlHelper.SourceKey("https://github.com/Owner/Repo/tree/feat/fix"));
        Assert.Equal(branch, RepoUrlHelper.SourceKey("https://github.com/owner/repo.git/tree/Feat%2FFix"));
        Assert.Contains("Feat/Fix", RepoUrlHelper.DeriveName("https://github.com/owner/repo/tree/Feat/Fix"));
    }
    [Theory]
    [InlineData("https://github.com/owner/repo/tree")]
    [InlineData("https://github.com/owner/repo/blob/main/File.cs")]
    [InlineData("https://github.com/owner/repo/issues")]
    public void InvalidSourceLinksAreNotSilentlyReducedToDefaultBranch(string input)
        => Assert.NotNull(RepoUrlHelper.FindProblem(input));

    [Fact]
    public async Task Single_template_request_is_rejected_before_cloning_anything()
    {
        var service = new TemplateGenerationService(
            Stub<IUnitOfWork>.Create((m, _) => throw new NotSupportedException(m.Name)),
            Stub<IGitCloneService>.Create((m, _) => throw new NotSupportedException("không được clone: " + m.Name)),
            Stub<IRepositoryScanner>.Create((m, _) => throw new NotSupportedException(m.Name)),
            [],
            Stub<IBusinessGroupingAi>.Create((m, _) => throw new NotSupportedException(m.Name)),
            NullLogger<TemplateGenerationService>.Instance);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.GenerateAsync(new GenerateTemplateRequest { RepoUrl = "https://github.com/WPD301-Sports-Center-Management-System" }));

        Assert.Contains("thiếu tên repo", ex.Message);
    }

    [Theory]
    [InlineData("Nhóm 1", new string[] { }, "Nhóm 1 (1)")]
    [InlineData("Nhóm 1", new string[] { "Nhóm 1" }, "Nhóm 1 (1)")]
    [InlineData("Nhóm 1", new string[] { "Nhóm 1", "Nhóm 1 (1)" }, "Nhóm 1 (2)")]
    [InlineData("Nhóm 1", new string[] { "Nhóm 1", "Nhóm 1 (1)", "Nhóm 1 (2)" }, "Nhóm 1 (3)")]
    [InlineData("Nhóm 1 (1)", new string[] { "Nhóm 1", "Nhóm 1 (1)" }, "Nhóm 1 (2)")]
    [InlineData("SWP391_Project", new string[] { "Other_Repo" }, "SWP391_Project (1)")]
    public void ComputeCopyName_formats_incremental_sequence_numbers_correctly(string baseName, string[] existing, string expected)
    {
        var result = RepoUrlHelper.ComputeCopyName(baseName, existing);
        Assert.Equal(expected, result);
    }
}
