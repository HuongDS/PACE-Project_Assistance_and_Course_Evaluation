using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PACE.Application.Analysis;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Common;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.ReadModels;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Implementations;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using Xunit;

namespace PACE.Tests;

public class RepositoryLinkTests
{
    [Fact] public async Task EditingLinkOnlyPreservesTemplateCommitAndOriginalAnalyzedSource()
    {
        var f = new Fixture(); var oldUrl = f.Run.RepoUrl;
        await f.Edit.UpdateAsync(f.Run.Id, new() { RepoUrl = "https://github.com/team/repo/tree/Feat/New", RepoName = "Repo" });
        Assert.EndsWith("/tree/Feat/New", f.Run.RepoUrl);
        Assert.Equal(oldUrl, f.Run.AnalyzedRepoUrl);
        Assert.Equal("old-commit", f.Run.AnalyzedCommitSha);
        Assert.Equal("old-template", f.Run.BusinessTemplateJson);
        Assert.Empty(f.Events);
    }
    [Fact] public async Task DuplicateUrlIsRejectedWithoutChangingLink()
    {
        var f = new Fixture { Duplicate = true }; var before = f.Run.RepoUrl;
        await Assert.ThrowsAsync<ConflictException>(() => f.Edit.UpdateAsync(f.Run.Id, new() { RepoUrl = "https://github.com/team/other" }));
        Assert.Equal(before, f.Run.RepoUrl);
    }
    [Fact] public async Task ExistingQuestionsRequireExplicitReplacement()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<ConflictException>(() => f.Analyze.CreateAndAnalyzeAsync(f.Request(false)));
        Assert.Empty(f.Events); Assert.Equal(0, f.CloneCalls);
    }
    [Fact] public async Task ConfirmedReplacementArchivesBeforeDeletingAndInsertingCurrentAnalysis()
    {
        var f = new Fixture();
        var result = await f.Analyze.CreateAndAnalyzeAsync(f.Request(true));
        Assert.Equal(f.Run.Id, result.AnalysisRunId);
        Assert.True(f.Events.IndexOf("archive") < f.Events.IndexOf("delete-businesses"));
        Assert.True(f.Events.IndexOf("archive") < f.Events.IndexOf("insert-methods"));
        Assert.Equal("new-commit", f.Run.AnalyzedCommitSha);
        Assert.Equal(f.Run.RepoUrl, f.Run.AnalyzedRepoUrl);
    }
    [Fact] public async Task FailedCloneCannotArchiveOrRemoveCurrentData()
    {
        var f = new Fixture { FailClone = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Analyze.CreateAndAnalyzeAsync(f.Request(true)));
        Assert.Empty(f.Events); Assert.Equal("old-template", f.Run.BusinessTemplateJson);
    }

    private sealed class Fixture
    {
        public AnalysisRun Run = new() { Id = Guid.NewGuid(), RepoUrl = "https://github.com/team/repo", RepoName = "Repo", BusinessTemplateJson="old-template", AnalyzedCommitSha="old-commit" };
        public List<string> Events = [];
        public bool Duplicate, FailClone;
        public int CloneCalls;
        public AnalysisRunService Edit;
        public RepoAnalysisService Analyze;
        public Fixture()
        {
            var runs = Stub<IAnalysisRunRepository>.Create((m,a) => m.Name switch {
                "GetByIdAsync" or "GetByRepoUrlAsync" => Task.FromResult<AnalysisRun?>(Run),
                "RepoUrlExistsAsync" => Task.FromResult(Duplicate),
                "HasQuestionsAsync" => Task.FromResult(true),
                "ArchiveCurrentAsync" => Event("archive"),
                "GetSummaryAsync" => Task.FromResult<AnalysisRunSummary?>(new(Run.Id,Run.RepoUrl,Run.RepoName,Run.CreatedAt,1,1,1)),
                _ => throw new InvalidOperationException(m.Name) });
            var users = Stub<IUserRepository>.Create((m,a) => Task.FromResult(new List<string>()));
            var businesses = Stub<IBusinessRepository>.Create((m,a) => m.Name switch {
                "DeleteByRepoAsync" => Delete("delete-businesses"), "AddRangeAsync" => Event("insert-businesses"), _ => throw new InvalidOperationException(m.Name) });
            var methods = Stub<IMethodSourceRepository>.Create((m,a) => m.Name switch {
                "DeleteByRepoAsync" => Delete("delete-methods"), "AddRangeAsync" => Event("insert-methods"), _ => throw new InvalidOperationException(m.Name) });
            var uow = Stub<IUnitOfWork>.Create((m,a) => m.Name switch {
                "get_AnalysisRuns" => runs, "get_Users" => users, "get_Businesses" => businesses, "get_MethodSources" => methods,
                "get_BusinessMethods" => Stub<IBusinessMethodRepository>.Create((m,a) => Task.CompletedTask),
                "get_BusinessFlows" => Stub<IBusinessFlowRepository>.Create((m,a) => Task.CompletedTask),
                "get_DataFlowEdges" => Stub<IDataFlowEdgeRepository>.Create((m,a) => Task.CompletedTask),
                "ExecuteInTransactionAsync" => ((Func<CancellationToken,Task>)a[0]!)(default), "SaveChangesAsync" => Task.FromResult(1),
                _ => throw new InvalidOperationException(m.Name) });
            var currentUser = Stub<ICurrentUserService>.Create((m, _) => m.Name switch
            {
                "get_IsAuthenticated" => true,
                "get_Role" => UserRole.Admin,
                _ => throw new NotImplementedException(m.Name)
            });
            Edit = new(uow, currentUser,
                Stub<IBusinessService>.Create((m,a) => Task.FromResult<IReadOnlyList<BusinessDto>>([])));
            var clone = Stub<IClonedRepository>.Create((m,a) => m.Name switch { "get_CommitSha" => "new-commit", "get_RootPath" or "get_WorkingPath" => "/tmp/fake", "Dispose" => null, _ => null });
            var git = Stub<IGitCloneService>.Create((m,a) => { CloneCalls++; if (FailClone) throw new InvalidOperationException("Clone failed"); return Task.FromResult(clone); });
            var scan = new RepositoryScan(); scan.CSharpFiles.Add(new("/tmp/fake/Test.cs","Test.cs","source"));
            var scanner = Stub<IRepositoryScanner>.Create((m,a) => m.Name == "Scan" ? scan : throw new InvalidOperationException(m.Name));
            var parsed = new ParseResult(); parsed.Methods.Add(new ParsedMethod { ClassName="TestController",MethodName="Get",FilePath="Test.cs",Language=ProgrammingLanguage.CSharp,SourceCode="return value;",HttpVerb="GET",Route="/test" });
            var parser = Stub<ILanguageParser>.Create((m,a) => m.Name switch { "get_Kind" => SourceKind.CSharp, "Parse" => parsed, "get_Name" => "Test", _ => null });
            Analyze = new(uow,git,scanner,[parser],Options.Create(new AnalysisOptions()),NullLogger<RepoAnalysisService>.Instance);
        }
        private Task Event(string name) { Events.Add(name); return Task.CompletedTask; }
        private Task<int> Delete(string name) { Events.Add(name); return Task.FromResult(0); }
        public CreateAnalysisRunRequest Request(bool replace) => new() {RepoUrl=Run.RepoUrl!,ReplaceExisting=replace,Businesses=[new BusinessTemplate { BusinessName="Tests",Apis=[new ApiTemplate {Controller="TestController",Function="Get",HttpMethod="GET"}] }]};
    }
}
