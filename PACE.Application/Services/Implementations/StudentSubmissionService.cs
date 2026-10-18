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
        if (string.IsNullOrWhiteSpace(request.StudentCode) || string.IsNullOrWhiteSpace(request.Email))
            throw new BadRequestException("Mã số sinh viên và Email không được để trống.");

        var authenticated = await FindStudentAsync(ct);
        var email = request.Email.Trim().ToLowerInvariant();
        var studentCode = request.StudentCode.Trim();

        var user = await uow.Users.GetWithRepoAsync(authenticated.Id, asNoTracking: false, ct: ct);
        if (user is null || user.Role != UserRole.Student)
            throw new NotFoundException("Không tìm thấy sinh viên.");

        if (string.IsNullOrWhiteSpace(user.StudentCode)
            || !string.Equals(user.StudentCode.Trim(), studentCode, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Mã số sinh viên không khớp với phiên đăng nhập.");

        var userWithRepo = await uow.Users.GetWithRepoAsync(user.Id, ct: ct);
        var run = RequireRepository(userWithRepo ?? user);

        // Kiểm tra xem sinh viên đã nộp bài hoàn tất trước đó chưa
        var submitted = await uow.StudentSubmissions.GetSubmittedAsync(user.Id, run.Id, ct);
        if (submitted is not null)
            throw new ConflictException("Sinh viên đã hoàn thành và nộp bài cho repository này.");

        // Kiểm tra xem sinh viên có phiên làm bài đang dở dang không
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
        if (request.Answers is null || request.Answers.Count != 3
            || request.Answers.Any(a => a is null || a.BusinessId == Guid.Empty || a.QuestionId == Guid.Empty
                || string.IsNullOrWhiteSpace(a.AnswerText)))
            throw new BadRequestException("Phải gửi đúng 3 câu trả lời không rỗng với Business và Question hợp lệ.");
        if (request.Answers.Select(a => a.BusinessId).Distinct().Count() != 3)
            throw new BadRequestException("Phải chọn 3 Business khác nhau.");

        var student = await FindStudentAsync(ct);
        var submittedAt = DateTime.UtcNow;
        Guid submissionId = request.SubmissionId ?? Guid.NewGuid();

        // 1. Lưu bài nộp và 3 câu trả lời vào Database (Transaction nhanh < 200ms)
        var (submission, businesses) = await uow.ExecuteInTransactionAsync(async token =>
        {
            await uow.StudentSubmissions.LockUserAsync(student.Id, token);
            var user = await FindStudentAsync(token);
            if (user.Id != student.Id)
                throw new ConflictException("Thông tin sinh viên đã thay đổi. Vui lòng thử lại.");
            var run = RequireRepository(user);

            var existing = request.SubmissionId.HasValue
                ? await uow.StudentSubmissions.GetOwnedAsync(request.SubmissionId.Value, user.Id, token)
                : await uow.StudentSubmissions.GetActiveSessionAsync(user.Id, run.Id, token);

            if (request.SubmissionId.HasValue && existing is null)
                throw new NotFoundException("Submission not found for the authenticated student.");
            if (existing is not null && existing.AnalysisRunId != run.Id)
                throw new ConflictException("Submission belongs to a different repository assignment.");
            if (existing is not null && existing.SubmittedAt.HasValue)
                throw new ConflictException("Sinh viên đã nộp bài cho repository này.");

            if (existing is null && await uow.StudentSubmissions.ExistsAsync(user.Id, run.Id, token))
                throw new ConflictException("Sinh viên đã nộp bài cho repository này.");

            var bizList = await uow.StudentSubmissions.GetBusinessesWithQuestionsAsync(run.Id, token);

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
            subEntity.Answers = request.Answers.Select(a => new StudentAnswer
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SubmissionId = subEntity.Id,
                QuestionId = a.QuestionId,
                AnswerText = a.AnswerText.Trim(),
                CreatedAt = submittedAt
            }).ToList();

            if (existing is null)
            {
                uow.StudentSubmissions.Add(subEntity);
            }
            else
            {
                uow.StudentSubmissions.Update(subEntity);
            }

            return (subEntity, bizList);
        }, ct);

        // 2. Chấm điểm các câu hỏi bằng AI song song qua Task.WhenAll (Ngoài Database Transaction)
        var allFormats = Enum.GetValues<FeedbackFormat>();
        FeedbackFormat[] assignedFormats;
        if (request.Answers.Count == allFormats.Length)
        {
            assignedFormats = allFormats.ToArray();
            Random.Shared.Shuffle(assignedFormats);
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
                    ContextModel = ContextType.Raw
                };

                var jsonResult = await studentAnswerService.EvaluateStudentAnswerAsync(evalReq);
                parsed = JsonSerializer.Deserialize<StudentAnswerRes>(jsonResult, JsonOptions);
            }
            catch
            {
                logger?.LogWarning("AI evaluation unavailable for submission {SubmissionId}, question {QuestionId}; saved answers retained", submission.Id, a.QuestionId);
                // Fallback nếu có lỗi ngoại lệ từ LLM
            }

            return new EvaluatedAnswerDto
            {
                BusinessId = a.BusinessId,
                BusinessName = business.BusinessName,
                QuestionId = a.QuestionId,
                QuestionText = question.QuestionText,
                AnswerText = a.AnswerText.Trim(),
                AiEvaluation = parsed
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
