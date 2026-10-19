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

    public async Task<SurveySummaryResponseDto> GetSurveyResultsAsync(CancellationToken ct = default)
    {
        var criteria = await uow.RubricCriteria.GetAllOrderedAsync(ct);
        var paged = await uow.StudentSubmissions.GetPagedAsync(new StudentSubmissionQuery { Page = 1, PageSize = 500 }, ct);
        var submissions = paged.Items;

        var items = new List<SurveySubmissionItemDto>();
        var allRatings = new List<(Guid CriterionId, int Score)>();

        foreach (var sub in submissions)
        {
            var studentEmail = sub.User?.Email ?? "—";
            var studentCode = sub.User?.StudentCode;
            var repoName = sub.AnalysisRun?.RepoName;

            foreach (var answer in sub.Answers)
            {
                var scores = answer.RubricScores.Select(r => new SubmissionRubricScoreDto(
                    r.Id, r.CriterionId, r.Criterion?.Code ?? "", r.Criterion?.Name ?? "", r.Criterion?.Statement ?? "", r.Score
                )).ToList();

                double avg = scores.Count > 0 ? scores.Average(s => s.Score) : 0;

                foreach (var sc in scores)
                {
                    allRatings.Add((sc.CriterionId, sc.Score));
                }

                items.Add(new SurveySubmissionItemDto(
                    sub.Id,
                    answer.Id,
                    studentEmail,
                    studentCode,
                    repoName,
                    answer.Question?.Business?.BusinessName ?? "Nghiệp vụ",
                    answer.Question?.QuestionText ?? "Câu hỏi",
                    answer.AnswerText ?? "",
                    answer.AiFeedback,
                    answer.StudentReply,
                    avg,
                    sub.SubmittedAt ?? sub.CreatedAt,
                    scores
                ));
            }
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
            submissions.Count,
            items.Count,
            allRatings.Count,
            overallAvg,
            highest != null ? $"{highest.Name} ({highest.AverageScore:F1} ⭐)" : "Chưa có",
            lowest != null ? $"{lowest.Name} ({lowest.AverageScore:F1} ⭐)" : "Chưa có",
            criteriaStats,
            items
        );
    }

    public async Task<byte[]> ExportSurveyResultsToExcelAsync(CancellationToken ct = default)
    {
        var summary = await GetSurveyResultsAsync(ct);
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
        var submission = await uow.StudentSubmissions.GetByIdAsync(request.SubmissionId, ct)
            ?? throw new NotFoundException("Bài nộp", request.SubmissionId);

        if (submission.UserId != userId)
            throw new ForbiddenException("Bạn không có quyền gửi khảo sát cho bài làm này.");

        var criteria = (await uow.RubricCriteria.GetAllOrderedAsync(ct)).ToDictionary(c => c.Id);

        foreach (var answerInput in request.Answers)
        {
            var answer = submission.Answers.FirstOrDefault(a => a.Id == answerInput.StudentAnswerId)
                      ?? (answerInput.QuestionId != Guid.Empty ? submission.Answers.FirstOrDefault(a => a.QuestionId == answerInput.QuestionId) : null);
            if (answer is null) continue;

            if (!string.IsNullOrWhiteSpace(answerInput.StudentReply))
            {
                answer.StudentReply = answerInput.StudentReply.Trim();
            }

            foreach (var sc in answerInput.Scores)
            {
                if (!criteria.ContainsKey(sc.CriterionId)) continue;
                if (sc.Score < 1 || sc.Score > 5) throw new BadRequestException("Điểm khảo sát phải từ 1 đến 5.");

                var existing = answer.RubricScores.FirstOrDefault(s => s.CriterionId == sc.CriterionId);
                if (existing != null)
                {
                    existing.Score = sc.Score;
                }
                else
                {
                    answer.RubricScores.Add(new RubricScore
                    {
                        Id = Guid.NewGuid(),
                        StudentAnswerId = answer.Id,
                        CriterionId = sc.CriterionId,
                        Score = sc.Score
                    });
                }
            }
        }

        await uow.SaveChangesAsync(ct);
    }

    private async Task EnsureCodeAvailableAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        if (await uow.RubricCriteria.CodeExistsAsync(code, exceptId, ct))
            throw new ConflictException($"Mã tiêu chí {code} đã tồn tại.");
    }
}
