using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Dtos.StudentAnswer;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class StudentSubmissionService(
    IUnitOfWork uow,
    ICurrentUserService currentUser,
    IStudentAnswerService studentAnswerService, ILogger<StudentSubmissionService>? logger = null) : IStudentSubmissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    private void RequireRole(UserRole role)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new UnauthorizedException("Bạn chưa đăng nhập hoặc UserId không hợp lệ.");
        if (currentUser.Role != role)
            throw new ForbiddenException();
    }

    private async Task<User> FindStudentAsync(CancellationToken ct)
    {
        RequireRole(UserRole.Student);
        var user = await uow.Users.GetWithRepoAsync(currentUser.UserId, ct: ct);
        if (user is null)
            throw new UnauthorizedException("Tài khoản không còn tồn tại.");
        if (user.Role != UserRole.Student)
            throw new ForbiddenException();
        return user;
    }

    private static AnalysisRun RequireRepository(User user)
        => user.AnalysisRunId.HasValue && user.AnalysisRun is not null
           && !string.IsNullOrWhiteSpace(user.AnalysisRun.RepoUrl)
            ? user.AnalysisRun
            : throw new UnprocessableException("Sinh viên chưa được gán repository.");

    private static SubmissionRepositoryDto MapRepository(AnalysisRun run)
        => new(run.Id, run.RepoName, run.RepoUrl);

    public Task<StudentSubmissionJoinedDto> JoinAsync(JoinStudentSubmissionRequest request, CancellationToken ct = default)
        => uow.ExecuteInTransactionAsync(async token => {
            RequireRole(UserRole.Student);
            await uow.StudentSubmissions.LockUserAsync(currentUser.UserId, token);
            return await JoinCoreAsync(request, token);
        }, ct);

    private async Task<StudentSubmissionJoinedDto> JoinCoreAsync(JoinStudentSubmissionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.StudentCode))
            throw new BadRequestException("Mã số sinh viên không được để trống.");

        var authenticated = await FindStudentAsync(ct);
        var studentCode = request.StudentCode.Trim();

        var user = await uow.Users.GetWithRepoAsync(authenticated.Id, asNoTracking: false, ct: ct);
        if (user is null || user.Role != UserRole.Student)
            throw new NotFoundException("Không tìm thấy sinh viên.");

        if (string.IsNullOrWhiteSpace(user.StudentCode)
            || !string.Equals(user.StudentCode.Trim(), studentCode, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Mã số sinh viên không khớp với phiên đăng nhập.");

        var email = !string.IsNullOrWhiteSpace(request.Email)
            ? request.Email.Trim().ToLowerInvariant()
            : user.Email;

        var userWithRepo = await uow.Users.GetWithRepoAsync(user.Id, ct: ct);
        var run = RequireRepository(userWithRepo ?? user);

        // 1. Kiểm tra xem sinh viên có phiên làm bài đang dở dang không (ưu tiên phiên đang làm)
        var active = await uow.StudentSubmissions.GetActiveSessionAsync(user.Id, run.Id, ct);
        if (active is not null)
        {
            return new StudentSubmissionJoinedDto(
                active.Id,
                active.CreatedAt,
                user.StudentCode ?? studentCode,
                email,
                run.RepoName
            );
        }

        // 2. Nếu không có phiên đang làm, kiểm tra xem sinh viên đã nộp bài hoàn tất trước đó chưa
        var submitted = await uow.StudentSubmissions.GetSubmittedAsync(user.Id, run.Id, ct);
        if (submitted is not null)
            return new StudentSubmissionJoinedDto(submitted.Id, submitted.CreatedAt,
                user.StudentCode!, email, run.RepoName) { SubmittedAt = submitted.SubmittedAt };

        // Tạo mới phiên làm bài (StudentSubmission với SubmittedAt = null)
        var submission = new StudentSubmission
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            AnalysisRunId = run.Id,
            CreatedAt = DateTime.UtcNow,
            SubmittedAt = null
        };

        uow.StudentSubmissions.Add(submission);
        await uow.SaveChangesAsync(ct);

        return new StudentSubmissionJoinedDto(
            submission.Id,
            submission.CreatedAt,
            user.StudentCode ?? studentCode,
            email,
            run.RepoName
        );
    }

    public async Task<StudentSubmissionLookupDto> LookupAsync(CancellationToken ct = default)
    {
        var run = RequireRepository(await FindStudentAsync(ct));
        var businesses = await uow.StudentSubmissions.GetBusinessesWithQuestionsAsync(run.Id, ct);
        return new(MapRepository(run), businesses.Select(b => new SubmissionBusinessDto(b.Id, b.BusinessName,
            b.Questions.OrderBy(q => q.Id).Select(q => new SubmissionQuestionDto(q.Id, q.QuestionText)).ToList())).ToList());
    }

    public async Task<StudentSubmissionResultDto> SubmitAsync(SubmitStudentSubmissionRequest request, CancellationToken ct = default)
    {
        RequireRole(UserRole.Student);
        if (request.Answers is null || request.Answers.Count == 0
            || request.Answers.Any(a => a is null || a.BusinessId == Guid.Empty || a.QuestionId == Guid.Empty
                || string.IsNullOrWhiteSpace(a.AnswerText)))
            throw new BadRequestException("Phải gửi các câu trả lời không rỗng với Business và Question hợp lệ.");
        if (request.Answers.Select(a => a.QuestionId).Distinct().Count() != request.Answers.Count)
            throw new BadRequestException("Không được chọn trùng câu hỏi trong bài làm.");

        var student = await FindStudentAsync(ct);
        var submittedAt = DateTime.UtcNow;
        Guid submissionId = request.SubmissionId ?? Guid.NewGuid();

        // 1. Lưu bài nộp và các câu trả lời vào Database (Transaction nhanh < 200ms)
        var (submission, businesses, alreadySubmitted) = await uow.ExecuteInTransactionAsync(async token =>
        {
            await uow.StudentSubmissions.LockUserAsync(student.Id, token);
            var user = await FindStudentAsync(token);
            if (user.Id != student.Id)
                throw new ConflictException("Thông tin sinh viên đã thay đổi. Vui lòng thử lại.");
            var run = RequireRepository(user);

            var existing = request.SubmissionId.HasValue
                ? await uow.StudentSubmissions.GetOwnedAsync(request.SubmissionId.Value, user.Id, token)
                : await uow.StudentSubmissions.GetActiveSessionAsync(user.Id, run.Id, token)
                    ?? await uow.StudentSubmissions.GetSubmittedAsync(user.Id, run.Id, token);

            if (request.SubmissionId.HasValue && existing is null)
                throw new NotFoundException("Submission not found for the authenticated student.");
            if (existing is not null && existing.AnalysisRunId != run.Id)
                throw new ConflictException("Submission belongs to a different repository assignment.");
            if (existing is not null && existing.SubmittedAt.HasValue)
                return (existing, new List<Business>(), true);

            if (existing is null && await uow.StudentSubmissions.ExistsAsync(user.Id, run.Id, token))
                throw new ConflictException("Sinh viên đã nộp bài cho repository này.");

            var bizList = await uow.StudentSubmissions.GetBusinessesWithQuestionsAsync(run.Id, token);
            var totalQuestionsCount = bizList.SelectMany(b => b.Questions).Count();
            var expectedCount = Math.Min(4, totalQuestionsCount);
            if (expectedCount > 0 && request.Answers.Count != expectedCount)
            {
                throw new BadRequestException(
                    expectedCount >= 4
                        ? "Quy chế bài thi yêu cầu bạn phải chọn và trả lời đúng 4 câu hỏi."
                        : $"Repository của bạn có tổng cộng {expectedCount} câu hỏi, do đó bài thi yêu cầu bạn phải chọn và trả lời toàn bộ {expectedCount} câu hỏi.");
            }

            foreach (var answer in request.Answers)
            {
                var question = bizList.SelectMany(b => b.Questions).FirstOrDefault(q => q.Id == answer.QuestionId);
                if (question is null)
                    throw new BadRequestException("Câu hỏi không tồn tại hoặc không thuộc repository của bài làm.");

                var business = bizList.FirstOrDefault(b => b.Id == answer.BusinessId);
                if (business is null || question.BusinessId != business.Id)
                    throw new BadRequestException("Business không thuộc repository hoặc Question không thuộc Business đã chọn.");
            }

            var subEntity = existing ?? new StudentSubmission
            {
                Id = submissionId,
                UserId = user.Id,
                AnalysisRunId = run.Id,
                CreatedAt = submittedAt
            };

            subEntity.SubmittedAt = submittedAt;
            var savedAnswers = existing?.Answers.ToDictionary(a => a.QuestionId) ?? new Dictionary<Guid, StudentAnswer>();
            subEntity.Answers = request.Answers.Select(a =>
            {
                savedAnswers.TryGetValue(a.QuestionId, out var previous);
                var text = a.AnswerText.Trim();
                var unchanged = previous?.AnswerText == text;
                return new StudentAnswer
                {
                    Id = previous?.Id ?? Guid.NewGuid(),
                    UserId = user.Id, SubmissionId = subEntity.Id, QuestionId = a.QuestionId,
                    AnswerText = text, CreatedAt = previous?.CreatedAt ?? submittedAt,
                    AiFeedback = unchanged ? previous?.AiFeedback : null,
                    FeedbackFormat = unchanged ? previous?.FeedbackFormat : null
                };
            }).ToList();

            if (existing is null)
            {
                uow.StudentSubmissions.Add(subEntity);
            }
            else
            {
                await uow.StudentSubmissions.UpdateAsync(subEntity, token);
            }

            return (subEntity, bizList, false);
        }, ct);

        if (alreadySubmitted) return MapSavedResult(submission);

        // 2. Chấm điểm các câu hỏi bằng AI song song qua Task.WhenAll (Ngoài Database Transaction)
        var allFormats = Enum.GetValues<FeedbackFormat>();
        FeedbackFormat[] assignedFormats;
        if (request.Answers.Count == allFormats.Length)
        {
            assignedFormats = allFormats.ToArray();
            Random.Shared.Shuffle(assignedFormats);
        }
        else if (request.Answers.Count < allFormats.Length)
        {
            var shuffled = allFormats.ToArray();
            Random.Shared.Shuffle(shuffled);
            assignedFormats = shuffled.Take(request.Answers.Count).ToArray();
        }
        else
        {
            assignedFormats = Enumerable.Range(0, request.Answers.Count)
                .Select(_ => allFormats[Random.Shared.Next(allFormats.Length)])
                .ToArray();
        }

        var evaluatedAnswers = new List<EvaluatedAnswerDto>();
        for (var index = 0; index < request.Answers.Count; index++)
        {
            var a = request.Answers[index];
            evaluatedAnswers.Add(await EvaluateAsync(a, index));
        }

        async Task<EvaluatedAnswerDto> EvaluateAsync(StudentAnswerRequest a, int index)
        {
            var business = businesses.First(b => b.Id == a.BusinessId);
            var question = business.Questions.First(q => q.Id == a.QuestionId);
            var format = assignedFormats[index];

            StudentAnswerRes? parsed = null;
            string? evaluationError = null;
            try
            {
                var evalReq = new StudentAnswerReq
                {
                    BusinessId = a.BusinessId,
                    QuestionId = a.QuestionId,
                    SubmissionId = submission.Id,
                    UserId = student.Id,
                    StudentAnswer = a.AnswerText.Trim(),
                    FeedbackFormat = format,
                    ContextModel = question.ContextType
                };

                var jsonResult = await studentAnswerService.EvaluateStudentAnswerAsync(evalReq);
                parsed = JsonSerializer.Deserialize<StudentAnswerRes>(jsonResult, JsonOptions);
                if (parsed?.HasContent != true)
                    throw new ExternalServiceException("AI", "Dịch vụ trả về feedback rỗng hoặc không đúng cấu trúc.");
            }
            catch (Exception ex)
            {
                parsed = null;
                evaluationError = ex is ExternalServiceException ? ex.Message : "Chưa tạo được feedback AI. Câu trả lời đã được lưu.";
                logger?.LogWarning("AI evaluation unavailable for submission {SubmissionId}, question {QuestionId}: {ErrorType}, {Reason}",
                    submission.Id, a.QuestionId, ex.GetType().Name, evaluationError);
            }

            var answerEntity = submission.Answers.FirstOrDefault(ans => ans.QuestionId == a.QuestionId);
            if (parsed is not null && answerEntity is not null)
            {
                parsed.StudentAnswerId = answerEntity.Id;
            }

            return new EvaluatedAnswerDto
            {
                StudentAnswerId = answerEntity?.Id ?? Guid.Empty,
                BusinessId = a.BusinessId,
                BusinessName = business.BusinessName,
                QuestionId = a.QuestionId,
                QuestionText = question.QuestionText,
                AnswerText = a.AnswerText.Trim(),
                ContextType = question.ContextType,
                AiEvaluation = parsed,
                EvaluationError = evaluationError
            };
        }

        var runInfo = RequireRepository(student);
        return new StudentSubmissionResultDto
        {
            SubmissionId = submission.Id,
            CreatedAt = submission.CreatedAt,
            SubmittedAt = submittedAt,
            Repository = MapRepository(runInfo),
            EvaluatedAnswers = evaluatedAnswers.ToList()
        };
    }

    public async Task<StudentSubmissionDetailDto> RetrieveAsync(RetrieveStudentSubmissionRequest request, CancellationToken ct = default)
    {
        RequireRole(UserRole.Student);
        if (request.SubmissionId == Guid.Empty) throw new BadRequestException("SubmissionId không hợp lệ.");
        var user = await FindStudentAsync(ct);
        var submission = await uow.StudentSubmissions.GetOwnedAsync(request.SubmissionId, user.Id, ct)
            ?? throw new NotFoundException("Không tìm thấy bài nộp của sinh viên.");
        return MapDetail(submission);
    }

    public async Task<PagedResult<AdminStudentSubmissionDto>> GetAllAsync(StudentSubmissionQuery query, CancellationToken ct = default)
    {
        RequireRole(UserRole.Admin);
        var submissions = await uow.StudentSubmissions.GetPagedAsync(query, ct);
        return submissions.Map(s => new AdminStudentSubmissionDto(s.UserId, s.User.StudentCode, s.User.Email, MapDetail(s)));
    }

    public async Task<ManagerStatisticsDto> GetManagerStatisticsAsync(CancellationToken ct = default)
    {
        RequireRole(UserRole.Admin);
        var allSubmitted = await uow.StudentSubmissions.GetAllSubmittedForStatsAsync(ct);

        static bool IsSurveyCompleted(StudentSubmission s) =>
            s.Answers.Count > 0 && s.Answers.All(a => a.RubricScores.Count > 0);

        var successfulSubmissions = allSubmitted.Where(IsSurveyCompleted).ToList();
        var pendingEvaluationSubmissions = allSubmitted.Where(s => !IsSurveyCompleted(s)).ToList();

        var totalSurveyedStudents = successfulSubmissions.Select(s => s.UserId).Distinct().Count();

        var dailyStats = successfulSubmissions
            .GroupBy(s => (s.SubmittedAt ?? s.CreatedAt).ToLocalTime().ToString("yyyy-MM-dd"))
            .OrderByDescending(g => g.Key)
            .Select(g => new DailySubmissionStatDto
            {
                Date = g.Key,
                SuccessfulSubmissionsCount = g.Count()
            })
            .ToList();

        var pendingList = pendingEvaluationSubmissions
            .OrderByDescending(s => s.SubmittedAt ?? s.CreatedAt)
            .Select(s => new PendingEvaluationDto
            {
                SubmissionId = s.Id,
                UserId = s.UserId,
                StudentCode = s.User?.StudentCode,
                Email = s.User?.Email ?? "—",
                RepoName = s.AnalysisRun?.RepoName,
                RepoUrl = s.AnalysisRun?.RepoUrl,
                SubmittedAt = s.SubmittedAt,
                TotalQuestions = s.Answers.Count,
                SurveyCompletedQuestions = s.Answers.Count(a => a.RubricScores.Count > 0)
            })
            .ToList();

        return new ManagerStatisticsDto
        {
            TotalSubmissions = allSubmitted.Count,
            TotalSuccessfulSubmissions = successfulSubmissions.Count,
            TotalPendingEvaluationSubmissions = pendingEvaluationSubmissions.Count,
            TotalSurveyedStudents = totalSurveyedStudents,
            DailySuccessfulSubmissions = dailyStats,
            PendingEvaluationSubmissions = pendingList
        };
    }

    public async Task<AdminStudentSubmissionDto> QuickCreateAsync(QuickCreateStudentSubmissionRequest request, CancellationToken ct = default)
    {
        RequireRole(UserRole.Admin);

        if (string.IsNullOrWhiteSpace(request.StudentCode))
            throw new BadRequestException("Mã số sinh viên không được để trống.");
        if (request.AnalysisRunId == Guid.Empty)
            throw new BadRequestException("Vui lòng chọn repository hợp lệ.");

        var run = await uow.AnalysisRuns.GetByIdAsync(request.AnalysisRunId, ct)
                  ?? throw new NotFoundException("Repository không tồn tại.");

        var studentCode = request.StudentCode.Trim().ToUpperInvariant();
        var email = !string.IsNullOrWhiteSpace(request.Email)
            ? request.Email.Trim().ToLowerInvariant()
            : $"{studentCode.ToLowerInvariant()}@fpt.edu.vn";

        var user = await uow.Users.GetByStudentCodeAsync(studentCode, ct);
        if (user is not null)
        {
            user = await uow.Users.GetWithRepoAsync(user.Id, asNoTracking: false, ct: ct) ?? user;
            user.Role = UserRole.Student;
            user.AnalysisRunId = run.Id;
            user.AnalysisRun = run;

            if (!string.IsNullOrWhiteSpace(request.Email) && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                if (await uow.Users.EmailExistsAsync(email, user.Id, ct))
                    throw new ConflictException("Email đã được sử dụng bởi tài khoản khác.");
                user.Email = email;
            }
            uow.Users.Update(user);
        }
        else
        {
            if (await uow.Users.EmailExistsAsync(email, null, ct))
                throw new ConflictException($"Email {email} đã được sử dụng bởi tài khoản khác. Vui lòng cung cấp email riêng cho sinh viên này.");

            user = new User
            {
                Id = Guid.NewGuid(),
                StudentCode = studentCode,
                Email = email,
                Role = UserRole.Student,
                AnalysisRunId = run.Id,
                AnalysisRun = run,
                CreatedAt = DateTime.UtcNow
            };
            await uow.Users.AddAsync(user, ct);
        }

        // Tạo hoặc lấy phiên bài làm đang dở dang cho sinh viên
        var active = await uow.StudentSubmissions.GetActiveSessionAsync(user.Id, run.Id, ct);
        StudentSubmission submission;
        if (active is not null)
        {
            submission = active;
        }
        else
        {
            submission = new StudentSubmission
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                AnalysisRunId = run.Id,
                CreatedAt = DateTime.UtcNow,
                SubmittedAt = null
            };
            uow.StudentSubmissions.Add(submission);
        }

        await uow.SaveChangesAsync(ct);

        var full = await uow.StudentSubmissions.GetByIdAsync(submission.Id, ct) ?? submission;
        full.User = user;
        full.AnalysisRun = run;

        return new AdminStudentSubmissionDto(user.Id, user.StudentCode, user.Email, MapDetail(full));
    }

    public async Task<StudentSubmissionDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new UnauthorizedException("Bạn chưa đăng nhập.");
        if (id == Guid.Empty) throw new BadRequestException("SubmissionId không hợp lệ.");
        StudentSubmission? submission;
        if (currentUser.Role == UserRole.Admin)
            submission = await uow.StudentSubmissions.GetByIdAsync(id, ct);
        else
        {
            RequireRole(UserRole.Student);
            submission = await uow.StudentSubmissions.GetOwnedAsync(id, currentUser.UserId, ct);
        }
        return MapDetail(submission ?? throw new NotFoundException("Không tìm thấy bài nộp."));
    }

    public async Task<PagedResult<StudentSubmissionDetailDto>> GetMineAsync(StudentSubmissionQuery query, CancellationToken ct = default)
    {
        RequireRole(UserRole.Student);
        // Always scope in the service, even if a caller supplies another UserId.
        var ownedQuery = new StudentSubmissionQuery
        {
            UserId = currentUser.UserId,
            AnalysisRunId = query.AnalysisRunId,
            IsSubmitted = query.IsSubmitted,
            Search = query.Search,
            Page = query.Page,
            PageSize = query.PageSize
        };
        return (await uow.StudentSubmissions.GetPagedAsync(ownedQuery, ct)).Map(MapDetail);
    }

    private static StudentSubmissionResultDto MapSavedResult(StudentSubmission submission)
        => new()
        {
            SubmissionId = submission.Id, CreatedAt = submission.CreatedAt, SubmittedAt = submission.SubmittedAt,
            Repository = MapRepository(submission.AnalysisRun),
            EvaluatedAnswers = submission.Answers.OrderBy(a => a.Question.Business.BusinessName).ThenBy(a => a.QuestionId)
                .Select(a => new EvaluatedAnswerDto
                {
                    BusinessId = a.Question.BusinessId, BusinessName = a.Question.Business.BusinessName,
                    QuestionId = a.QuestionId, QuestionText = a.Question.QuestionText, AnswerText = a.AnswerText ?? "",
                    AiEvaluation = ParseSavedFeedback(a.AiFeedback)
                }).ToList()
        };

    private static StudentAnswerRes? ParseSavedFeedback(string? feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback)) return null;
        try
        {
            var parsed = JsonSerializer.Deserialize<StudentAnswerRes>(feedback, JsonOptions);
            return parsed?.HasContent == true ? parsed : null;
        }
        catch (JsonException) { return new StudentAnswerRes { Comment = feedback }; }
    }

    private static StudentSubmissionDetailDto MapDetail(StudentSubmission submission)
        => new(submission.Id, submission.CreatedAt, submission.SubmittedAt, MapRepository(submission.AnalysisRun),
            submission.Answers.OrderBy(a => a.Question.Business.BusinessName).ThenBy(a => a.QuestionId)
                .Select(a => new SubmittedAnswerDto(a.Question.BusinessId, a.Question.Business.BusinessName,
                    a.QuestionId, a.Question.QuestionText, a.AnswerText, a.AiFeedback)
                {
                    StudentAnswerId = a.Id,
                    SuggestedAnswer = a.Question.SuggestedAnswer,
                    Difficulty = a.Question.Difficulty,
                    ContextType = a.Question.ContextType,
                    QuestionCreatedAt = a.Question.CreatedAt,
                    AnswerCreatedAt = a.CreatedAt,
                    FeedbackFormat = a.FeedbackFormat,
                    StudentReply = a.StudentReply,
                    RubricScores = a.RubricScores.OrderBy(r => r.Criterion.DisplayOrder).ThenBy(r => r.CriterionId)
                        .Select(r => new SubmissionRubricScoreDto(r.Id, r.CriterionId, r.Criterion.Code,
                            r.Criterion.Name, r.Criterion.Statement, r.Score)).ToList()
                }).ToList())
        {
            Student = new(submission.UserId, submission.User.StudentCode, submission.User.Email)
        };
}
