using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.ReadModels;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.QuestionGeneration;
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
    ILogger<QuestionGenerationService> logger) : IQuestionGenerationService
{
    // Chuỗi thay thế khe ngữ cảnh bị ẩn — giữ nguyên văn như Repo_Into_Graph (QuestionGenerate.cs).
    private const string RawModeContextPlaceholder =
        "Chế độ Truyền thống: Không cung cấp sơ đồ Mermaid Graph. Hãy phân tích trực tiếp từ Source Code.";
    private const string GraphModeCodePlaceholder =
        "Chế độ Graph: Không cung cấp mã nguồn thô. Hãy phân tích logic dựa hoàn toàn vào sơ đồ Mermaid Graph được cung cấp.";
    private const string NoFlowContext =
        "Không có luồng nghiệp vụ (Feature) nào được map với Business này.";

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

        // Không có mã nguồn thì DỪNG — nếu không Gemini sẽ bịa câu hỏi chỉ từ tên nghiệp vụ.
        if (methods.Count == 0)
            throw new BadRequestException("Không tìm thấy Source Code nào được map cho Business này để sinh câu hỏi.");

        // 2. N_w = số node (hàm) phân biệt trong các luồng của nghiệp vụ — đo được trước khi gọi LLM.
        var nodeCount = CountNodes(flows);
        var threshold = _options.GraphNodeThreshold;
        var route = QuestionContextRouter.Resolve(request.Mode, nodeCount, threshold);

        var hasGraph = flows.Any(f => !string.IsNullOrWhiteSpace(f.MermaidGraph));
        if (route.ContextType == ContextType.Graph && !hasGraph)
            throw new BadRequestException("Nghiệp vụ này chưa có sơ đồ Mermaid nào nên không sinh được câu hỏi ở mode Graph. Hãy dùng Raw hoặc phân tích lại repo.");

        // 3. Dựng hai khe ngữ cảnh rồi ẩn khe không dùng (giống QuestionGenerate của Repo_Into_Graph).
        var codeBuilder = BuildCode(methods, flows);
        var contextBuilder = BuildFlowContext(flows);
        if (route.ContextType == ContextType.Raw) contextBuilder = RawModeContextPlaceholder;
        else codeBuilder = GraphModeCodePlaceholder;

        logger.LogInformation(
            "Sinh {Count} câu hỏi cho nghiệp vụ {BusinessId} ({BusinessName}): mode={Mode}, N_w={NodeCount}, ngưỡng={Threshold} → {Context}",
            request.NumberOfQuestions, business.Id, business.BusinessName, request.Mode, nodeCount, threshold, route.ContextType);

        // 4. Gọi Gemini với prompt nguyên văn.
        var prompt = QuestionPromptBuilder.Build(
            businessName: business.BusinessName,
            codeBuilder: codeBuilder,
            contextBuilder: contextBuilder,
            numberOfQuestions: request.NumberOfQuestions,
            difficulty: difficulty,
            additionalContext: request.Description);

        var result = await llm.GenerateJsonAsync(prompt.SystemInstruction, prompt.UserPrompt, _options.Temperature, ct);
        var generated = LlmQuestionParser.Parse(result.Text).Take(request.NumberOfQuestions).ToList();
        if (generated.Count == 0)
            throw new ExternalServiceException("Gemini", "Phản hồi hợp lệ nhưng không có câu hỏi nào.");

        // 5. Lưu DB (append).
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

        // Chỉ thêm mới — câu hỏi cũ của nghiệp vụ được giữ nguyên.
        await uow.GeneratedQuestions.AddRangeAsync(entities, ct);
        await uow.SaveChangesAsync(ct);

        var questionDtos = entities
            .Select((e, i) => e.ToDto(targetedEntryPoints: generated[i].TargetedEntryPoints ?? []))
            .ToList();

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
            questionDtos);
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
    public static int CountNodes(IEnumerable<FlowQuestionContext> flows)
    {
        var nodes = new HashSet<Guid>();
        foreach (var flow in flows)
        {
            nodes.Add(flow.EntryMethodId);
            foreach (var edge in flow.Edges)
            {
                nodes.Add(edge.FromMethodId);
                nodes.Add(edge.ToMethodId);
            }
        }
        return nodes.Count;
    }

    /// <summary>Mã nguồn thô, xếp theo thứ tự xuất hiện trong luồng (entry → các bước), phần còn lại theo tên.</summary>
    private static string BuildCode(IReadOnlyList<MethodCode> methods, IReadOnlyList<FlowQuestionContext> flows)
    {
        var order = new Dictionary<Guid, int>();
        foreach (var flow in flows)
        {
            order.TryAdd(flow.EntryMethodId, order.Count);
            foreach (var edge in flow.Edges)
            {
                order.TryAdd(edge.FromMethodId, order.Count);
                order.TryAdd(edge.ToMethodId, order.Count);
            }
        }

        var sb = new StringBuilder();
        foreach (var method in methods.OrderBy(m => order.TryGetValue(m.Id, out var i) ? i : int.MaxValue))
        {
            sb.AppendLine($"// Class: {method.ClassName}, Method: {method.MethodName}");
            sb.AppendLine(method.SourceCode);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildFlowContext(IReadOnlyList<FlowQuestionContext> flows)
    {
        if (flows.Count == 0) return NoFlowContext + Environment.NewLine;

        var sb = new StringBuilder();
        foreach (var flow in flows)
        {
            sb.AppendLine($"### Tên luồng: {flow.Name}");
            if (!string.IsNullOrWhiteSpace(flow.MermaidGraph))
            {
                sb.AppendLine("Data Mermaid Diagram:");
                sb.AppendLine(flow.MermaidGraph);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
