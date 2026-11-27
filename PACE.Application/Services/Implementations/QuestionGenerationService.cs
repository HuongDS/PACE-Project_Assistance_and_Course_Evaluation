using PACE.Application.Analysis.Models;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.ReadModels;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.QuestionGeneration;
using PACE.Application.QuestionGeneration.Drawio;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

/// <summary>
/// Sinh câu hỏi vấn đáp cho một nghiệp vụ theo 3 mode:
///   Raw    : chỉ mã nguồn thô (arm Traditional trong bài báo 1)
///   Graph  : chỉ sơ đồ Mermaid (arm Pure CFG)
///   Hybrid : N_w &gt; ngưỡng (mặc định 10) → Graph, ngược lại → Raw
/// Prompt dùng chung cho mọi mode (chép nguyên từ Repo_Into_Graph), chỉ khác nội dung hai khe
/// ngữ cảnh — đúng thiết kế thực nghiệm của bài báo. Kết quả được lưu vào bảng generated_questions.
/// </summary>
public class QuestionGenerationService(
    IUnitOfWork uow,
    IQuestionLlmClient llm,
    IOptions<QuestionGenerationOptions> options,
    ILogger<QuestionGenerationService> logger,
    IDrawioSpecificationStorage? specStorage = null) : IQuestionGenerationService
{
    private const int DifficultyMaxLength = 20;

    private readonly QuestionGenerationOptions _options = options.Value;

    public async Task<GenerateQuestionsResponse> GenerateAsync(Guid businessId, GenerateQuestionsRequest request, CancellationToken ct = default)
    {
        if (request.NumberOfQuestions < 1 || request.NumberOfQuestions > _options.MaxQuestions)
            throw new BadRequestException($"Số câu hỏi phải nằm trong khoảng 1 - {_options.MaxQuestions}.");
        if (string.IsNullOrWhiteSpace(request.Difficulty))
            throw new BadRequestException("Độ khó không được để trống.");

        var difficulty = request.Difficulty.Trim();
        var business = await uow.Businesses.GetByIdAsync(businessId, ct)
                       ?? throw new NotFoundException("nghiệp vụ", businessId);

        // 1. Ngữ cảnh: các luồng (Mermaid + cạnh) và mã nguồn của nghiệp vụ.
        var flows = await uow.BusinessFlows.GetQuestionContextsByBusinessAsync(businessId, ct);
        var methods = await uow.MethodSources.GetCodeByBusinessAsync(businessId, ct);

        // Không có mã nguồn thì DỪNG — nếu không OpenAI sẽ bịa câu hỏi chỉ từ tên nghiệp vụ.
        if (methods.Count == 0)
            throw new BadRequestException("Không tìm thấy Source Code nào được map cho Business này để sinh câu hỏi.");

        // 2. N_w = số node (hàm) phân biệt trong các luồng của nghiệp vụ — đo được trước khi gọi LLM.
        var nodeCount = CountNodes(flows);
        var threshold = _options.GraphNodeThreshold;
        var route = QuestionContextRouter.Resolve(request.Mode, nodeCount, threshold);

        // 3. Dựng hai khe ngữ cảnh rồi ẩn khe không dùng (giống QuestionGenerate của Repo_Into_Graph).
        var context = QuestionContextComposer.Compose(methods, flows, route);

        // 3.5. Kiểm tra và nạp Sơ đồ nghiệp vụ chuẩn (Ground Truth từ Draw.io) nếu có.
        string? groundTruthSpec = null;
        if (specStorage != null)
        {
            var specDiagram = await specStorage.GetSpecificationAsync(businessId, ct);
            if (specDiagram != null && !string.IsNullOrWhiteSpace(specDiagram.NormalizedFlowText))
            {
                groundTruthSpec = specDiagram.NormalizedFlowText;
                logger.LogInformation("Áp dụng Sơ đồ nghiệp vụ chuẩn Ground Truth ({DiagramName}) cho Business {BusinessId}",
                    specDiagram.DiagramName, business.Id);
            }
        }

        logger.LogInformation(
            "Sinh {Count} câu hỏi cho nghiệp vụ {BusinessId} ({BusinessName}): mode={Mode}, N_w={NodeCount}, ngưỡng={Threshold}, GroundTruth={HasGroundTruth} → {Context}",
            request.NumberOfQuestions, business.Id, business.BusinessName, request.Mode, nodeCount, threshold, groundTruthSpec != null, route.ContextType);

        // 4. Gọi OpenAI với prompt có neo tri thức.
        var prompt = QuestionPromptBuilder.Build(
            businessName: business.BusinessName,
            codeBuilder: context.Code,
            contextBuilder: context.Flow,
            numberOfQuestions: request.NumberOfQuestions,
            difficulty: difficulty,
            additionalContext: request.Description,
            groundTruthSpecification: groundTruthSpec,
            hasFrontend: context.HasFrontend);

        var result = await llm.GenerateJsonAsync(prompt.SystemInstruction, prompt.UserPrompt, _options.Temperature, ct);
        var generated = LlmQuestionParser.Parse(result.Text).Take(request.NumberOfQuestions).ToList();
        if (generated.Count == 0)
            throw new ExternalServiceException("OpenAI", "Phản hồi hợp lệ nhưng không có câu hỏi nào.");

        // 5. Lưu DB.
        var entities = generated.Select(q => new GeneratedQuestion
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            QuestionText = q.Question.Trim(),
            SuggestedAnswer = string.IsNullOrWhiteSpace(q.SuggestedAnswer) ? null : q.SuggestedAnswer.Trim(),
            Difficulty = Truncate(string.IsNullOrWhiteSpace(q.Difficulty) ? difficulty : q.Difficulty.Trim(), DifficultyMaxLength),
            ContextType = route.ContextType,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        var deleted = await uow.ExecuteInTransactionAsync(async token =>
        {
            var removed = request.ReplaceExisting
                ? await uow.GeneratedQuestions.DeleteUnansweredByBusinessAsync(business.Id, token)
                : 0;
            await uow.GeneratedQuestions.AddRangeAsync(entities, token);
            return removed;
        }, ct);

        var questionDtos = entities
            .Select((e, i) => e.ToDto(answerCount: 0, targetedEntryPoints: generated[i].TargetedEntryPoints ?? []))
            .ToList();

        var fullPromptText = $"=== SYSTEM INSTRUCTION ===\n{prompt.SystemInstruction}\n\n=== USER PROMPT ===\n{prompt.UserPrompt}";

        return new GenerateQuestionsResponse(
            business.Id,
            business.BusinessName,
            request.Mode,
            route.ContextType,
            nodeCount,
            threshold,
            route.Reason,
            flows.Count,
            methods.Count,
            result.Model,
            result.InputTokens,
            result.OutputTokens,
            result.LatencyMs,
            deleted,
            questionDtos,
            HasGroundTruth: groundTruthSpec != null,
            PromptText: fullPromptText);
    }

    public async Task<IReadOnlyList<GeneratedQuestionDto>> GetByBusinessAsync(Guid businessId, CancellationToken ct = default)
    {
        if (!await uow.Businesses.AnyAsync(b => b.Id == businessId, ct)) throw new NotFoundException("nghiệp vụ", businessId);
        return (await uow.GeneratedQuestions.GetSummariesByBusinessAsync(businessId, ct)).Select(q => q.ToDto()).ToList();
    }

    public async Task DeleteAsync(Guid questionId, CancellationToken ct = default)
    {
        var question = await uow.GeneratedQuestions.GetByIdAsync(questionId, ct)
                       ?? throw new NotFoundException("câu hỏi", questionId);
        if (await uow.GeneratedQuestions.CountAnswersAsync(questionId, ct) > 0)
            throw new ConflictException("Câu hỏi đã có sinh viên trả lời, không thể xoá.");

        uow.GeneratedQuestions.Remove(question);
        await uow.SaveChangesAsync(ct);
    }

    /// <summary>Số hàm phân biệt xuất hiện trong các luồng (entry + hai đầu mỗi cạnh) = N_w trong bài báo.</summary>
    public static int CountNodes(IEnumerable<FlowQuestionContext> flows) => QuestionContextComposer.CountNodes(flows);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
