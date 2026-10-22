using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PACE.Application.Analysis;
using PACE.Application.Common.Exceptions;
using PACE.Infrastructure.Analysis;
using Xunit;

namespace PACE.Tests;

public class GitBranchCloneTests
{
    [Fact] public void SlashInBranchIsNotMistakenForFolder()
    {
        var resolved = GitCloneService.ResolveBranchPath("Feat/FixUnhappyCase/backend", ["main", "Feat/FixUnhappyCase"]);
        Assert.Equal("Feat/FixUnhappyCase", resolved.Branch);
        Assert.Equal("backend", resolved.SubFolder);
        Assert.Throws<BadRequestException>(() => GitCloneService.ResolveBranchPath("feat/FixUnhappyCase", ["Feat/FixUnhappyCase"]));
    }

    [Theory]
    [InlineData("/tree/Feat/FixUnhappyCase", "feature-code")]
    [InlineData("/tree/Feat%2FFixUnhappyCase", "feature-code")]
    [InlineData("/tree/main", "main-code")]
    [InlineData("", "main-code")]
    public async Task ActualCloneReadsTheRequestedBranch(string suffix, string expected)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = new LocalGit();
        using var clone = await fixture.Service.CloneAsync("https://github.com/test/source" + suffix);
        Assert.Equal(expected, await File.ReadAllTextAsync(Path.Combine(clone.WorkingPath, "backend", "Code.cs")));
        Assert.Equal(40, clone.CommitSha!.Length);
        var path = clone.RootPath;
        clone.Dispose();
        Assert.False(Directory.Exists(path));
    }
    [Fact] public async Task ExistingFolderIsReadFromTheSelectedBranch()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = new LocalGit();
        using var clone = await fixture.Service.CloneAsync("https://github.com/test/source/tree/Feat/FixUnhappyCase/backend");
        Assert.Equal("feature-code", await File.ReadAllTextAsync(Path.Combine(clone.WorkingPath, "Code.cs")));
    }
    [Fact] public async Task MissingBranchFailsBeforeCloneInsteadOfUsingMain()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = new LocalGit();
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.CloneAsync("https://github.com/test/source/tree/not-found"));
        Assert.DoesNotContain("clone", File.ReadAllLines(fixture.Calls));
    }
    [Fact] public async Task FailedBranchCloneDoesNotRetryDefaultBranch()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = new LocalGit(failClone: true);
        await Assert.ThrowsAsync<ExternalServiceException>(() => fixture.Service.CloneAsync("https://github.com/test/source/tree/Feat/FixUnhappyCase"));
        Assert.Equal(1, File.ReadAllLines(fixture.Calls).Count(x => x == "clone"));
    }
    [Fact] public async Task MissingFolderDoesNotFallBackToWholeRepository()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = new LocalGit();
        await Assert.ThrowsAsync<BadRequestException>(() => fixture.Service.CloneAsync("https://github.com/test/source/tree/Feat/FixUnhappyCase/missing"));
        Assert.Empty(Directory.GetDirectories(fixture.Clones));
    }

    private sealed class LocalGit : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "pace-branch-test-" + Guid.NewGuid().ToString("N"));
        public string Calls => Path.Combine(root, "calls");
        public string Clones => Path.Combine(root, "clones");
        public GitCloneService Service { get; }
        public LocalGit(bool failClone = false)
        {
            if (OperatingSystem.IsWindows()) throw new NotSupportedException();
            var source = Path.Combine(root, "source");Directory.CreateDirectory(source);
            Git(source, "init", "--initial-branch=main");
            Directory.CreateDirectory(Path.Combine(source, "backend"));
            File.WriteAllText(Path.Combine(source, "backend", "Code.cs"), "main-code");
            Git(source, "add", ".");Git(source, "-c", "user.name=Test", "-c", "user.email=test@example.com", "commit", "-m", "main");
            Git(source, "checkout", "-b", "Feat/FixUnhappyCase");
            File.WriteAllText(Path.Combine(source, "backend", "Code.cs"), "feature-code");
            Git(source, "add", ".");Git(source, "-c", "user.name=Test", "-c", "user.email=test@example.com", "commit", "-m", "feature");
            Git(source, "checkout", "main");
            var wrapper = Path.Combine(root, "git-wrapper");
            static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";
            File.WriteAllText(wrapper, "#!/bin/sh\nprintf '%s\\n' \"$1\" >> " + Quote(Calls) + "\n"
                + (failClone ? "if [ \"$1\" = clone ]; then echo 'simulated transport failure' >&2; exit 1; fi\n" : "")
                + "exec git -c protocol.file.allow=always -c " + Quote("url.file://" + source + ".insteadOf=https://github.com/test/source.git") + " \"$@\"\n");
            File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Service = new(Options.Create(new AnalysisOptions {GitExecutable = wrapper, CloneRoot = Clones}), NullLogger<GitCloneService>.Instance);
        }
        private static void Git(string directory, params string[] arguments)
        {
            var info = new ProcessStartInfo("git") {WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true};
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info)!;
            var stdout = process.StandardOutput.ReadToEnd();var stderr = process.StandardError.ReadToEnd();process.WaitForExit();
            Assert.True(process.ExitCode == 0, stdout + stderr);
        }
        public void Dispose() {if (Directory.Exists(root)) Directory.Delete(root, true);}
    }
}
