using PACE.Application.Analysis.Flow;
using PACE.Application.Analysis.Template;
using PACE.Application.QuestionGeneration;
using PACE.Infrastructure.Analysis;
using Xunit;

namespace PACE.Tests;

public class StaticFrontendTests
{
    [Fact]
    public void StaticSiteWithoutPackageManifestHasMatchableFrontendEntries()
    {
        var root = Directory.CreateTempSubdirectory("pace-static-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "index.html"), """<script src="quiz.js"></script>""");
            File.WriteAllText(Path.Combine(root.FullName, "quiz.js"), """if (!answer) show("Chưa chọn đáp án");""");

            var scan = new RepositoryScanner().Scan(root.FullName, default);
            var entries = scan.FrontendEntries().ToList();
            var templates = BusinessTemplateGenerator.GroupFrontend(entries);
            var quiz = Assert.Single(templates, t => t.BusinessName.EndsWith("quiz.js"));
            var matcher = new EntryPointMatcher(entries);
            var method = matcher.Match(Assert.Single(quiz.Apis), out _);

            Assert.Equal(2, scan.TotalFiles);
            Assert.Empty(scan.JsTsFiles);
            Assert.NotNull(method);
            Assert.Contains("Chưa chọn đáp án", method.SourceCode);
            var prompt = QuestionPromptBuilder.Build(quiz.BusinessName, method.SourceCode, "", 1, "trung bình", hasFrontend: true);
            Assert.Contains("người dùng sẽ thấy gì", prompt.SystemInstruction);
            Assert.Contains("Chưa chọn đáp án", prompt.UserPrompt);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
