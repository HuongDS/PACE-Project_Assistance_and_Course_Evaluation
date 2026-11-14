using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;
namespace PACE.API.Controllers;

[ApiController, Route("api/evidence/projects/{projectId:guid}"), Authorize]
public class EvidenceController(AppDbContext db, IGitCloneService git, TeachingAccess access) : ControllerBase
{
    private async Task<string> Repository(Guid id, CancellationToken ct)
    {
        var user = await access.User(ct); var run = await db.AnalysisRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Repository unavailable.");
        if (user.Role == UserRole.Lecturer) { await access.Project(id, ct); } else if (user.Role != UserRole.Admin && user.AnalysisRunId != id) throw new ForbiddenException();
        return run.RepoUrl ?? throw new UnprocessableException("Repository URL unavailable.");
    }
    private static async Task<string> ReadGit(string directory, IEnumerable<string> args, CancellationToken ct)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("--no-pager"); foreach (var arg in args) start.ArgumentList.Add(arg);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0"; using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var p = Process.Start(start) ?? throw new ExternalServiceException("git", "Cannot start Git.");
        var error = p.StandardError.ReadToEndAsync(timeout.Token);
        var text = new StringBuilder(); var buffer = new char[4096];
        try { int count; while ((count = await p.StandardOutput.ReadAsync(buffer.AsMemory(), timeout.Token)) > 0) { text.Append(buffer, 0, count); if (text.Length > 1_000_000) throw new UnprocessableException("Evidence exceeds 1 MB response limit; narrow the selection."); } await p.WaitForExitAsync(timeout.Token); await error; if (p.ExitCode != 0) throw new ExternalServiceException("git", "Repository evidence unavailable."); return text.ToString(); }
        finally { if (!p.HasExited) p.Kill(true); }
    }
    private static async Task<string[]> DiffArguments(string directory, string commitSha, bool patch, CancellationToken ct)
    {
        var lineage = (await ReadGit(directory, ["rev-list", "--parents", "-n", "1", commitSha], ct)).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return lineage.Length > 1
         ? ["diff", patch ? "--patch" : "--numstat", "--no-ext-diff", "--no-renames", lineage[1], lineage[0], "--"]
         : ["diff-tree", "--root", "--no-commit-id", "-r", patch ? "--patch" : "--numstat", "--no-ext-diff", "--no-renames", commitSha, "--"];
    }
    public static (List<GitCommitView> Commits, List<GitContributionView> Contributions) ParseGitCommitsAndContributions(string logOutput)
    {
        var lines = logOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var commits = new List<GitCommitView>();

        string? sha = null, authorEmail = null, authorName = null, message = null, timestamp = null;
        int additions = 0, deletions = 0, changedFilesCount = 0;

        void FlushCurrentCommit()
        {
            if (sha != null)
            {
                commits.Add(new GitCommitView(
                    Sha: sha,
                    AuthorEmail: authorEmail ?? "",
                    AuthorName: authorName ?? "",
                    Message: message ?? "",
                    Timestamp: timestamp ?? "",
                    Additions: additions,
                    Deletions: deletions,
                    ChangedFilesCount: changedFilesCount
                ));
                sha = null; authorEmail = null; authorName = null; message = null; timestamp = null;
                additions = 0; deletions = 0; changedFilesCount = 0;
            }
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("COMMIT\0"))
            {
                FlushCurrentCommit();
                var fields = line.Substring("COMMIT\0".Length).Split('\0');
                if (fields.Length >= 5)
                {
                    sha = fields[0];
                    authorEmail = fields[1];
                    authorName = fields[2];
                    message = fields[3];
                    timestamp = fields[4];
                }
            }
            else if (sha != null)
            {
                var parts = line.Split('\t');
                if (parts.Length >= 3)
                {
                    changedFilesCount++;
                    if (int.TryParse(parts[0], out var add)) additions += add;
                    if (int.TryParse(parts[1], out var del)) deletions += del;
                }
            }
        }
        FlushCurrentCommit();

        var totalCommits = commits.Count;
        var totalLoc = commits.Sum(c => c.Additions + c.Deletions);

        var contributions = commits
            .GroupBy(c =>
            {
                var email = c.AuthorEmail.Trim().ToLowerInvariant();
                return string.IsNullOrEmpty(email) ? c.AuthorName.Trim().ToLowerInvariant() : email;
            })
            .Select(g =>
            {
                var first = g.First();
                var name = string.IsNullOrWhiteSpace(first.AuthorName) ? (first.AuthorEmail.Length > 0 ? first.AuthorEmail : "Unknown") : first.AuthorName;
                var email = first.AuthorEmail;
                var commitCount = g.Count();
                var totalAdd = g.Sum(c => c.Additions);
                var totalDel = g.Sum(c => c.Deletions);
                var totalFiles = g.Sum(c => c.ChangedFilesCount);
                var commitRatio = totalCommits > 0 ? Math.Round((double)commitCount / totalCommits * 100, 1) : 0;
                var locRatio = totalLoc > 0 ? Math.Round((double)(totalAdd + totalDel) / totalLoc * 100, 1) : 0;

                return new GitContributionView(
                    AuthorName: name,
                    AuthorEmail: email,
                    CommitCount: commitCount,
                    Additions: totalAdd,
                    Deletions: totalDel,
                    ChangedFilesCount: totalFiles,
                    CommitRatio: commitRatio,
                    LocRatio: locRatio
                );
            })
            .OrderByDescending(c => c.CommitCount)
            .ThenByDescending(c => c.Additions)
            .ToList();

        return (commits, contributions);
    }

    [ProducesResponseType(typeof(EvidenceView), 200)]
    [HttpGet]
    public async Task<IActionResult> Bundle(Guid projectId, CancellationToken ct)
    {
        var url = await Repository(projectId, ct); using var clone = await git.CloneAsync(url, ct);
        var logOutput = await ReadGit(clone.RootPath, ["log", "-n", "50", "--numstat", "--format=COMMIT%x00%H%x00%ae%x00%an%x00%s%x00%aI"], ct);
        var (commits, contributions) = ParseGitCommitsAndContributions(logOutput);
        var analysis = await db.AnalysisRuns.AsNoTracking().Where(r => r.Id == projectId).Select(r => new { r.AnalyzedCommitSha, r.AnalyzedAt }).SingleAsync(ct);
        var currentCommitSha = commits.Count > 0 ? commits[0].Sha : "";

        return Ok(new EvidenceView(
            ProjectId: projectId,
            RepositoryUrl: url,
            Commits: commits,
            Contributions: contributions,
            AnalyzedAt: DateTime.UtcNow,
            Coverage: "Repository commits history and member contribution metrics computed from up to 50 recent commits.",
            AnalyzedCommitSha: analysis.AnalyzedCommitSha,
            AnalysisTimestamp: analysis.AnalyzedAt,
            CurrentCommitSha: currentCommitSha
        ));
    }
    [ProducesResponseType(typeof(CommitDiffView[]), 200)]
    [HttpGet("commits/{commitSha}/diffs")]
    public async Task<IActionResult> Diffs(Guid projectId, string commitSha, CancellationToken ct)
    {
        var url = await Repository(projectId, ct); if (!Regex.IsMatch(commitSha, "\\A[0-9a-fA-F]{40}\\z")) return BadRequest(new { error = "Full 40-character commit SHA required." });
        using var clone = await git.CloneAsync(url, ct);
        bool commitExists = false;
        try
        {
            var rev = (await ReadGit(clone.RootPath, ["rev-parse", "--verify", $"{commitSha}^{{commit}}"], ct)).Trim();
            commitExists = !string.IsNullOrEmpty(rev);
        }
        catch (ExternalServiceException)
        {
            commitExists = false;
        }
        if (!commitExists) return NotFound(new { error = "Commit outside evidence snapshot; commit not found in repository." });
        var patch = await ReadGit(clone.RootPath, await DiffArguments(clone.RootPath, commitSha, true, ct), ct);
        // Return actual unified patches; no inferred per-file metrics or synthetic source.
        var files = Regex.Split(patch, "(?m)(?=^diff --git )").Where(x => x.StartsWith("diff --git ")).Take(100).Select(x => new { filename = Regex.Match(x, "(?m)^\\+\\+\\+ b/(.*)$").Groups[1].Value is { Length: > 0 } name ? name : Regex.Match(x, "(?m)^--- a/(.*)$").Groups[1].Value, status = x.Contains("new file mode ") ? "added" : x.Contains("deleted file mode ") ? "deleted" : "modified", additions = x.Split('\n').Count(l => l.StartsWith('+') && !l.StartsWith("+++")), deletions = x.Split('\n').Count(l => l.StartsWith('-') && !l.StartsWith("---")), patch = x }).ToArray();
        return Ok(files);
    }
    [ProducesResponseType(typeof(AstView), 200)]
    [HttpGet("ast-cfg")]
    public async Task<IActionResult> Ast(Guid projectId, [FromQuery, Required, MaxLength(1000)] string filePath, [FromQuery] string graphKind = "AST", CancellationToken ct = default)
    {
        await Repository(projectId, ct);
        if (graphKind is not ("AST" or "CFG")) return BadRequest(new { error = "graphKind must be AST or CFG." });
        // Only exact persisted analyzer paths are queried; client paths never reach the filesystem.
        var sources = await db.MethodSources.AsNoTracking().Where(m => m.AnalysisRunId == projectId && m.FilePath == filePath).OrderBy(m => m.Id).Take(101).ToListAsync(ct);
        if (sources.Count == 0) return NotFound(new { error = "No persisted method analysis for this exact file path." });
        if (sources.Count > 100 || sources.Sum(x => x.SourceCode.Length) > 500000) return UnprocessableEntity(new { error = "Source exceeds AST response limits." });
        if (sources.Any(x => x.Language != ProgrammingLanguage.CSharp)) return UnprocessableEntity(new { error = "AST/CFG endpoint currently supports persisted C# methods only." });
        var nodes = new List<object>(); var edges = new List<object>();
        if (graphKind == "CFG")
        {
            foreach (var source in sources)
            {
                var tree = CSharpSyntaxTree.ParseText("using System; class __PaceFragment { " + source.SourceCode + " }", cancellationToken: ct);
                var compilation = CSharpCompilation.Create("PaceEvidence", [tree], [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                if (compilation.GetDiagnostics(ct).Any(d => d.Severity == DiagnosticSeverity.Error)) return UnprocessableEntity(new { error = "Verified CFG requires a self-contained C# method. This fragment needs project types or references unavailable in persisted analysis." });
                var method = tree.GetRoot(ct).DescendantNodes().OfType<MethodDeclarationSyntax>().SingleOrDefault();
                if (method is null) return UnprocessableEntity(new { error = "Verified CFG supports method declarations only." });
                var cfg = ControlFlowGraph.Create(method, compilation.GetSemanticModel(tree), ct);
                if (cfg is null) return UnprocessableEntity(new { error = "Roslyn could not create a verified method CFG." });
                if (nodes.Count + cfg.Blocks.Length > 2000) return UnprocessableEntity(new { error = "CFG exceeds 2000 node limit." });
                foreach (var block in cfg.Blocks)
                {
                    var id = $"{source.Id}:block:{block.Ordinal}";
                    var label = $"{source.MethodName} / {block.Kind} #{block.Ordinal}: " + string.Join("; ", block.Operations.Select(o => o.Syntax.ToString()));
                    if (block.BranchValue is not null) label += "; condition/value: " + block.BranchValue.Syntax;
                    nodes.Add(new { id, label, type = "statement" });
                    if (block.FallThroughSuccessor?.Destination is { } next) edges.Add(new { source = id, target = $"{source.Id}:block:{next.Ordinal}", relation = "fall-through" });
                    if (block.ConditionalSuccessor?.Destination is { } conditional) edges.Add(new { source = id, target = $"{source.Id}:block:{conditional.Ordinal}", relation = "conditional" });
                }
            }
            var provenance = await db.AnalysisRuns.AsNoTracking().Where(r => r.Id == projectId).Select(r => new { r.AnalyzedCommitSha, r.AnalyzedAt }).SingleAsync(ct);
            return Ok(new { nodes, edges, analyzedCommitSha = provenance.AnalyzedCommitSha, analysisTimestamp = provenance.AnalyzedAt, cyclomaticComplexity = (int?)null, graphKind = "CFG", coverage = "Verified Roslyn basic blocks and successor edges for self-contained persisted C# methods. Project-dependent fragments return 422. No full-project CFG or inferred complexity." });
        }

        foreach (var source in sources) { var root = SyntaxFactory.ParseMemberDeclaration(source.SourceCode); if (root is null || root.ContainsDiagnostics) return UnprocessableEntity(new { error = "Persisted C# fragment cannot be parsed as a member; verified AST unavailable." }); ct.ThrowIfCancellationRequested(); var all = root.DescendantNodesAndSelf().Take(2001).ToArray(); if (nodes.Count + all.Length > 2000) return UnprocessableEntity(new { error = "AST exceeds 2000 node limit." }); var ids = all.Select((n, i) => (n, id: $"{source.Id}:{i}")).ToDictionary(x => x.n, x => x.id); foreach (var n in all) { nodes.Add(new { id = ids[n], label = n.Kind().ToString(), type = "statement" }); if (n.Parent != null && ids.TryGetValue(n.Parent, out var parent)) edges.Add(new { source = parent, target = ids[n], relation = "syntax-child" }); } }
        var analysis = await db.AnalysisRuns.AsNoTracking().Where(r => r.Id == projectId).Select(r => new { r.AnalyzedCommitSha, r.AnalyzedAt }).SingleAsync(ct);
        return Ok(new { nodes, edges, analyzedCommitSha = analysis.AnalyzedCommitSha, analysisTimestamp = analysis.AnalyzedAt, cyclomaticComplexity = (int?)null, graphKind = "AST", coverage = "Roslyn syntax trees of persisted C# method fragments. No full-file semantic model or control-flow graph." });
    }
}
