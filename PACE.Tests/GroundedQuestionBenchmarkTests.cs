using System.Text.Json;
using System.Text.Json.Serialization;
using PACE.Application.Common.ReadModels;
using PACE.Application.QuestionGeneration;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;

namespace PACE.Tests;

public sealed class GroundedQuestionBenchmarkTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record Scenario(string Id, string Source, int Budget, string[] Expected,
        bool OmitFlow = false, bool StaleSource = false);

    [Fact]
    public void CompareGeneratedQuestionsWithFrozenSourceOracle()
    {
        var fixture = Environment.GetEnvironmentVariable("PACE_GROUNDED_QUESTIONS_FIXTURE");
        if (string.IsNullOrWhiteSpace(fixture)) return;
        var scenarios = JsonSerializer.Deserialize<Scenario[]>(File.ReadAllText(fixture), Json)!;
        Assert.NotEmpty(scenarios);

        var report = scenarios.Select(scenario =>
        {
            var path = scenario.Id + ".java";
            var result = new JavaTreeSitterParser().Parse([new(path, path, scenario.Source)], default);
            var parsed = Assert.Single(result.Methods);
            var input = new MethodCode(parsed.Id, parsed.ClassName, parsed.MethodName,
                scenario.StaleSource ? parsed.SourceCode.Split('\n')[0] : parsed.SourceCode,
                path, scenario.OmitFlow ? null : parsed.JavaFlow?.Serialize());
            var questions = GroundedQuestionGenerator.Generate([input], scenario.Budget);
            var repeated = GroundedQuestionGenerator.Generate([input, input], scenario.Budget);
            var observed = questions.Select(Key).ToArray();
            var duplicates = questions.Count - questions.DistinctBy(
                q => (q.FilePath, q.Anchor, q.Kind, q.Variable)).Count();
            var invalidCitations = questions.Count(q => !q.Question.Contains(Reference(q, q.Anchor), StringComparison.Ordinal) ||
                q.Targets.Any(target => !q.Answer.Contains(Reference(q, target), StringComparison.Ordinal)));
            return new
            {
                scenario.Id,
                scenario.Budget,
                scenario.Expected,
                Observed = observed,
                Questions = questions,
                GraphStatus = parsed.JavaFlow?.Status,
                Warnings = result.Warnings,
                Valid = observed.Intersect(scenario.Expected, StringComparer.Ordinal).Count(),
                Incorrect = observed.Except(scenario.Expected, StringComparer.Ordinal).Count(),
                MissingExpected = scenario.Expected.Except(observed, StringComparer.Ordinal).Count(),
                Unfilled = scenario.Budget - questions.Count,
                InvalidCitations = invalidCitations,
                Duplicates = duplicates,
                Stable = JsonSerializer.Serialize(questions, Json) == JsonSerializer.Serialize(repeated, Json),
                Exact = scenario.Expected.Order(StringComparer.Ordinal).SequenceEqual(observed.Order(StringComparer.Ordinal))
            };
        }).ToArray();

        var output = Environment.GetEnvironmentVariable("PACE_GROUNDED_QUESTIONS_REPORT");
        if (!string.IsNullOrWhiteSpace(output))
            File.WriteAllText(output, JsonSerializer.Serialize(report, Json) + "\n");

        Assert.All(report, row =>
        {
            Assert.True(row.Exact, $"{row.Id}: {string.Join(", ", row.Observed)}");
            Assert.Equal(0, row.InvalidCitations);
            Assert.Equal(0, row.Duplicates);
            Assert.True(row.Stable);
        });
    }

    private static string Key(GroundedQuestion question)
    {
        var targets = string.Join(",", question.Targets.Select(site => site.Line).Distinct().Order());
        var variable = question.Variable is null ? "" : $"{question.Variable}:";
        return $"{question.Kind}:{question.Anchor.Line}:{variable}{targets}";
    }

    private static string Reference(GroundedQuestion question, PACE.Application.Analysis.Models.CallSite site) =>
        $"{question.FilePath}:{site.Line}:{site.Column}";
}
