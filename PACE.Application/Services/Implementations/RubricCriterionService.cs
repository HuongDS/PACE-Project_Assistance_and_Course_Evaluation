using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PACE.Application.Services.Implementations;

public class RubricCriterionService(IUnitOfWork uow, ISurveyExcelService excelService) : IRubricCriterionService
{
    public async Task<IReadOnlyList<RubricCriterionDto>> GetAllAsync(CancellationToken ct = default)
        => (await uow.RubricCriteria.GetAllOrderedAsync(ct)).Select(c => c.ToDto()).ToList();

    public async Task<RubricCriterionDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        => (await uow.RubricCriteria.GetByIdAsync(id, ct) ?? throw new NotFoundException("tiêu chí", id)).ToDto();

    public async Task<RubricCriterionDto> CreateAsync(UpsertRubricCriterionRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureCodeAvailableAsync(code, null, ct);

        var criterion = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Statement = request.Statement.Trim(),
            DisplayOrder = request.DisplayOrder
        };
        await uow.RubricCriteria.AddAsync(criterion, ct);
        await uow.SaveChangesAsync(ct);
        return criterion.ToDto();
    }

    public async Task<RubricCriterionDto> UpdateAsync(Guid id, UpsertRubricCriterionRequest request, CancellationToken ct = default)
    {
        var criterion = await uow.RubricCriteria.GetByIdAsync(id, ct) ?? throw new NotFoundException("tiêu chí", id);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureCodeAvailableAsync(code, id, ct);

        criterion.Code = code;
        criterion.Name = request.Name.Trim();
        criterion.Statement = request.Statement.Trim();
        criterion.DisplayOrder = request.DisplayOrder;
        await uow.SaveChangesAsync(ct);
        return criterion.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var criterion = await uow.RubricCriteria.GetByIdAsync(id, ct) ?? throw new NotFoundException("tiêu chí", id);
        if (await uow.RubricCriteria.IsUsedAsync(id, ct))
            throw new ConflictException("Tiêu chí đã có sinh viên chấm điểm, không thể xoá.");
        uow.RubricCriteria.Remove(criterion);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<BulkImportRubricCriteriaResult> BulkImportAsync(List<UpsertRubricCriterionRequest> items, CancellationToken ct = default)
    {
        if (items is null || items.Count == 0)
            throw new BadRequestException("Danh sách tiêu chí rỗng.");

        var existingCriteria = await uow.RubricCriteria.GetAllOrderedAsync(ct);
        var existingMap = existingCriteria.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        int created = 0;
        int updated = 0;
        var errors = new List<string>();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var code = item.Code?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add($"Dòng {i + 1}: Mã tiêu chí không được để trống.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                errors.Add($"Dòng {i + 1} ({code}): Tên tiêu chí không được để trống.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.Statement))
            {
                errors.Add($"Dòng {i + 1} ({code}): Nội dung câu hỏi khảo sát không được để trống.");
                continue;
            }

            if (existingMap.TryGetValue(code, out var existing))
            {
                existing.Name = item.Name.Trim();
                existing.Statement = item.Statement.Trim();
                if (item.DisplayOrder > 0) existing.DisplayOrder = item.DisplayOrder;
                updated++;
            }
            else
            {
                var criterion = new RubricCriterion
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    Name = item.Name.Trim(),
                    Statement = item.Statement.Trim(),
                    DisplayOrder = item.DisplayOrder > 0 ? item.DisplayOrder : (existingCriteria.Count + created + 1)
                };
                await uow.RubricCriteria.AddAsync(criterion, ct);
                existingMap[code] = criterion;
                created++;
            }
        }

        await uow.SaveChangesAsync(ct);
        return new BulkImportRubricCriteriaResult(items.Count, created, updated, errors);
    }

    public async Task<BulkImportRubricCriteriaResult> ImportFromStreamAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var items = excelService.ReadCriteriaFromExcel(stream);
        return await BulkImportAsync(items.ToList(), ct);
    }

    public Task<byte[]> GenerateCriteriaTemplateExcelAsync(CancellationToken ct = default)
        => Task.FromResult(excelService.GenerateCriteriaTemplate());

    public async Task<SurveyFilterOptions> GetSurveyFilterOptionsAsync(CancellationToken ct = default)
    {
        var answers = await uow.StudentSubmissions.GetSurveyAnswersAsync(ct);
        return new SurveyFilterOptions(
            answers.Select(a => a.Submission!.AnalysisRun).DistinctBy(r => r.Id)
                .OrderBy(r => r.RepoName).Select(r => new SurveyRepoOption(r.Id, r.RepoName ?? r.RepoUrl ?? "Repository")).ToList(),
            answers.Select(a => a.Question.Business).DistinctBy(b => b.Id).OrderBy(b => b.BusinessName)
                .Select(b => new SurveyBusinessOption(b.Id, b.AnalysisRunId, b.BusinessName)).ToList(),
            answers.Select(a => a.Question).DistinctBy(q => q.Id).OrderBy(q => q.QuestionText)
                .Select(q => new SurveyQuestionOption(q.Id, q.Business.AnalysisRunId, q.BusinessId, q.QuestionText)).ToList());
    }

    public async Task<SurveySummaryResponseDto> GetSurveyResultsAsync(SurveyResultQuery? query = null, CancellationToken ct = default)
    {
        query ??= new SurveyResultQuery();
        SurveyResultFiltering.Validate(query);
        var criteria = (await uow.RubricCriteria.GetAllOrderedAsync(ct))
            .Where(c => !query.CriterionId.HasValue || c.Id == query.CriterionId).ToList();
        var answers = await uow.StudentSubmissions.GetSurveyAnswersAsync(ct);
        var items = new List<SurveySubmissionItemDto>();
        var allRatings = new List<(Guid CriterionId, int Score)>();
        foreach (var answer in answers)
        {
            var sub = answer.Submission!;
            var scores = answer.RubricScores.Where(r => !query.CriterionId.HasValue || r.CriterionId == query.CriterionId)
                .Select(r => new SubmissionRubricScoreDto(r.Id, r.CriterionId, r.Criterion?.Code ?? "",
                    r.Criterion?.Name ?? "", r.Criterion?.Statement ?? "", r.Score)).ToList();
            var item = new SurveySubmissionItemDto(sub.Id, answer.Id, answer.User.Email, answer.User.StudentCode,
                sub.AnalysisRun.RepoName, answer.Question.Business.BusinessName, answer.Question.QuestionText,
                answer.AnswerText ?? "", answer.AiFeedback, answer.StudentReply,
                scores.Count > 0 ? scores.Average(s => s.Score) : 0, sub.SubmittedAt, scores)
            {
                AnalysisRunId = sub.AnalysisRunId, BusinessId = answer.Question.BusinessId,
                QuestionId = answer.QuestionId, FeedbackFormat = answer.FeedbackFormat,
                QuestionContextType = answer.Question.ContextType
            };
            if (!SurveyResultFiltering.Matches(item, query)) continue;
            items.Add(item);
            allRatings.AddRange(scores.Select(sc => (sc.CriterionId, sc.Score)));
        }

        var criteriaStats = new List<CriterionStatDto>();
        foreach (var c in criteria)
        {
            var cRatings = allRatings.Where(r => r.CriterionId == c.Id).Select(r => r.Score).ToList();
            double avg = cRatings.Count > 0 ? cRatings.Average() : 0;
            int s1 = cRatings.Count(s => s == 1);
            int s2 = cRatings.Count(s => s == 2);
            int s3 = cRatings.Count(s => s == 3);
            int s4 = cRatings.Count(s => s == 4);
            int s5 = cRatings.Count(s => s == 5);

            criteriaStats.Add(new CriterionStatDto(
                c.Id,
                c.Code,
                c.Name,
                c.Statement,
                avg,
                cRatings.Count,
                s1, s2, s3, s4, s5
            ));
        }

        double overallAvg = allRatings.Count > 0 ? allRatings.Average(r => r.Score) : 0;
        var highest = criteriaStats.Where(c => c.TotalRatings > 0).OrderByDescending(c => c.AverageScore).FirstOrDefault();
        var lowest = criteriaStats.Where(c => c.TotalRatings > 0).OrderBy(c => c.AverageScore).FirstOrDefault();

        return new SurveySummaryResponseDto(
            items.Select(i => i.SubmissionId).Distinct().Count(),
            items.Count,
            allRatings.Count,
            overallAvg,
            highest != null ? $"{highest.Name} ({highest.AverageScore:F1} ⭐)" : "Chưa có",
            lowest != null ? $"{lowest.Name} ({lowest.AverageScore:F1} ⭐)" : "Chưa có",
            criteriaStats,
            items
        );
    }

    public async Task<byte[]> ExportSurveyResultsToExcelAsync(SurveyResultQuery? query = null, CancellationToken ct = default)
    {
        var summary = await GetSurveyResultsAsync(query, ct);
        return excelService.ExportSurveyResults(summary);
    }

    public async Task<int> SeedSampleSurveysIfEmptyAsync(CancellationToken ct = default)
    {
        var criteria = await uow.RubricCriteria.GetAllOrderedAsync(ct);
        if (criteria.Count == 0) return 0;

        var paged = await uow.StudentSubmissions.GetPagedAsync(new StudentSubmissionQuery { Page = 1, PageSize = 100 }, ct);
        int seeded = 0;
        var sampleReplies = new[]
        {
            "Nhận xét của AI rất chi tiết, chỉ rõ luồng code thực tế trong repository.",
            "Tôi đồng ý với đánh giá này, các gợi ý cải thiện rất hữu ích.",
            "Đánh giá khá công bằng, giúp nhóm hiểu rõ hơn về giải thuật.",
            "Phần nhận xét về ngoại lệ và validate dữ liệu rất chính xác.",
            "Nhận xét rõ ràng, động viên tinh thần làm bài của sinh viên."
        };

        var rng = new Random(42);

        foreach (var sub in paged.Items)
        {
            foreach (var answer in sub.Answers)
            {
                if (answer.RubricScores.Count > 0) continue;

                if (string.IsNullOrEmpty(answer.StudentReply))
                {
                    answer.StudentReply = sampleReplies[rng.Next(sampleReplies.Length)];
                }

                foreach (var c in criteria)
                {
                    int score = rng.NextDouble() switch
                    {
                        < 0.15 => 3,
                        < 0.60 => 4,
                        _ => 5
                    };

                    answer.RubricScores.Add(new RubricScore
                    {
                        Id = Guid.NewGuid(),
                        StudentAnswerId = answer.Id,
                        CriterionId = c.Id,
                        Score = score
                    });
                    seeded++;
                }
            }
        }

        if (seeded > 0)
        {
            await uow.SaveChangesAsync(ct);
        }
        return seeded;
    }

    public async Task SubmitSurveyAsync(SubmitSurveyRequest request, Guid userId, CancellationToken ct = default)
    {
        await uow.ExecuteInTransactionAsync(async token =>
        {
            // Serialize retries before reading scores so concurrent requests update rather than duplicate.
            await uow.StudentSubmissions.LockUserAsync(userId, token);
            var submission = await uow.StudentSubmissions.GetForSurveyUpdateAsync(request.SubmissionId, token)
                ?? throw new NotFoundException("Bài nộp", request.SubmissionId);
            if (submission.UserId != userId)
                throw new ForbiddenException("Bạn không có quyền gửi khảo sát cho bài làm này.");
            if (submission.Answers.Any(a => a.RubricScores.Count > 0))
                throw new ConflictException("Khảo sát đã hoàn tất. Bạn chỉ có thể xem lại kết quả đã lưu.");
            if (submission.SubmittedAt == null)
                throw new BadRequestException("Vui lòng nộp bài trước khi gửi khảo sát.");
            if (request.Answers is null || request.Answers.Count == 0)
                throw new BadRequestException("Danh sách đánh giá khảo sát rỗng.");

            var criteria = (await uow.RubricCriteria.GetAllOrderedAsync(token)).Select(c => c.Id).ToHashSet();
            var changes = new List<(StudentAnswer Answer, StudentAnswerSurveyInputDto Input)>();
            var seenAnswers = new HashSet<Guid>();
            // Validate the whole payload before modifying tracked entities.
            foreach (var input in request.Answers)
            {
                var answer = input.StudentAnswerId != Guid.Empty
                    ? submission.Answers.FirstOrDefault(a => a.Id == input.StudentAnswerId)
                    : submission.Answers.FirstOrDefault(a => a.QuestionId == input.QuestionId);
                if (answer == null || (input.QuestionId != Guid.Empty && answer.QuestionId != input.QuestionId))
                    throw new BadRequestException("Câu trả lời không thuộc bài nộp này.");
                if (!seenAnswers.Add(answer.Id))
                    throw new BadRequestException("Câu trả lời bị lặp trong khảo sát.");
                if (input.Scores is null || input.Scores.Count == 0)
                    throw new BadRequestException("Chưa có điểm đánh giá tiêu chí.");
                var seenCriteria = new HashSet<Guid>();
                foreach (var score in input.Scores)
                {
                    if (!criteria.Contains(score.CriterionId) || !seenCriteria.Add(score.CriterionId))
                        throw new BadRequestException("Tiêu chí khảo sát không hợp lệ hoặc bị lặp.");
                    if (score.Score < 1 || score.Score > 5)
                        throw new BadRequestException("Điểm khảo sát phải từ 1 đến 5.");
                }
                changes.Add((answer, input));
            }

            if (changes.Count != submission.Answers.Count || changes.Any(change => !criteria.SetEquals(change.Input.Scores.Select(s => s.CriterionId))))
                throw new BadRequestException("Vui lòng đánh giá đủ tiêu chí cho tất cả câu trả lời trước khi gửi khảo sát.");

            foreach (var (answer, input) in changes)
            {
                if (input.StudentReply != null) answer.StudentReply = input.StudentReply.Trim();
                foreach (var score in input.Scores)
                {
                    var existing = answer.RubricScores.FirstOrDefault(s => s.CriterionId == score.CriterionId);
                    if (existing != null) existing.Score = score.Score;
                    else uow.StudentSubmissions.AddSurveyScore(new RubricScore
                    {
                        Id = Guid.NewGuid(), StudentAnswerId = answer.Id,
                        CriterionId = score.CriterionId, Score = score.Score
                    });
                }
            }
            await uow.SaveChangesAsync(token);
        }, ct);
    }

    private async Task EnsureCodeAvailableAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        if (await uow.RubricCriteria.CodeExistsAsync(code, exceptId, ct))
            throw new ConflictException($"Mã tiêu chí {code} đã tồn tại.");
    }
}
