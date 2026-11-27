using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PACE.Application.Common.ReadModels;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.QuestionGeneration;
using PACE.Application.Services.Implementations;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Analysis.Parsing;
using Xunit;

namespace PACE.Tests;

public sealed class GroundedQuestionServiceTests
{
    [Fact]
    public void UsesParserCoordinatesWhenUnicodePrecedesALocalRead()
    {
        const string path = "Unicode.java";
        const string code = "class Unicode { int run(int x) { /* tiếng Việt 😀 */ return x; } }";
        var method = Assert.Single(new JavaTreeSitterParser().Parse([new(path, path, code)], default).Methods);
        var question = Assert.Single(GroundedQuestionGenerator.Generate(
            [new(method.Id, method.ClassName, method.MethodName, method.SourceCode, path, method.JavaFlow?.Serialize())],
            1));
        Assert.Equal(GroundedQuestionKind.Definition, question.Kind);
        Assert.Equal("x", question.Variable);
    }

    [Theory]
    [InlineData(3, true, 3, 0, 7)]
    [InlineData(5, true, 3, 2, 0)]
    [InlineData(5, false, 0, 5, 0)]
    public async Task EvidenceModePersistsOnlyGeneratedQuestionsAndReplacesOnlyWhenFilled(
        int requested, bool withFlow, int generated, int unfilled, int removed)
    {
        const string path = "Choice.java";
        const string code = """
            class Choice {
              int run(boolean flag) {
                if (flag) return 1;
                return 2;
              }
            }
            """;
        var business = new Business { Id = Guid.NewGuid(), BusinessName = "Choice" };
        var method = Assert.Single(new JavaTreeSitterParser().Parse([new(path, path, code)], default).Methods);
        var methods = new List<MethodCode>
        {
            new(method.Id, method.ClassName, method.MethodName, method.SourceCode,
                path, withFlow ? method.JavaFlow?.Serialize() : null)
        };
        var saved = new List<GeneratedQuestion>();
        var businesses = Stub<IBusinessRepository>.Create((_, _) => Task.FromResult(business));
        var flows = Stub<IBusinessFlowRepository>.Create((_, _) => Task.FromResult(new List<FlowQuestionContext>()));
        var sources = Stub<IMethodSourceRepository>.Create((_, args) =>
        {
            Assert.True(Assert.IsType<bool>(args[2]));
            return Task.FromResult(methods);
        });
        var questions = Stub<IGeneratedQuestionRepository>.Create((method, args) =>
        {
            if (method.Name == nameof(IGeneratedQuestionRepository.DeleteUnansweredByBusinessAsync))
            {
                Assert.True(removed > 0);
                return Task.FromResult(removed);
            }
            Assert.Equal(nameof(IGeneratedQuestionRepository.AddRangeAsync), method.Name);
            saved.AddRange(Assert.IsAssignableFrom<IEnumerable<GeneratedQuestion>>(args[0]));
            return Task.CompletedTask;
        });
        var uow = Stub<IUnitOfWork>.Create((method, args) => method.Name switch
        {
            "get_Businesses" => businesses,
            "get_BusinessFlows" => flows,
            "get_MethodSources" => sources,
            "get_GeneratedQuestions" => questions,
            nameof(IUnitOfWork.ExecuteInTransactionAsync) when generated > 0 =>
                Assert.IsType<Func<CancellationToken, Task<int>>>(args[0])(default),
            _ => throw new InvalidOperationException(method.Name)
        });
        var llm = Stub<IQuestionLlmClient>.Create((_, _) => throw new InvalidOperationException("Evidence must run offline."));
        var service = new QuestionGenerationService(uow, llm, Options.Create(new QuestionGenerationOptions()),
            NullLogger<QuestionGenerationService>.Instance);

        var result = await service.GenerateAsync(business.Id, new GenerateQuestionsRequest
        {
            Mode = QuestionGenerationMode.Evidence, NumberOfQuestions = requested, Difficulty = "Dễ", ReplaceExisting = true
        });

        Assert.Equal(ContextType.Evidence, result.AppliedContext);
        Assert.Equal(unfilled, result.UnfilledCount);
        Assert.Equal(removed, result.DeletedCount);
        Assert.Equal(generated, result.Questions.Count);
        Assert.Equal(generated, saved.Count);
        Assert.All(saved, question => Assert.Equal(ContextType.Evidence, question.ContextType));
    }
}
