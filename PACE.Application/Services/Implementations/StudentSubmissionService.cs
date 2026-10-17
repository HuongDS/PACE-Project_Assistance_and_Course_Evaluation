using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class StudentSubmissionService(IUnitOfWork uow, ICurrentUserService currentUser) : IStudentSubmissionService
{
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

    public async Task<StudentSubmissionLookupDto> LookupAsync(CancellationToken ct = default)
    {
        var run = RequireRepository(await FindStudentAsync(ct));
        var businesses = await uow.StudentSubmissions.GetBusinessesWithQuestionsAsync(run.Id, ct);
        return new(MapRepository(run), businesses.Select(b => new SubmissionBusinessDto(b.Id, b.BusinessName,
            b.Questions.OrderBy(q => q.Id).Select(q => new SubmissionQuestionDto(q.Id, q.QuestionText)).ToList())).ToList());
    }

    public async Task<StudentSubmissionCreatedDto> SubmitAsync(SubmitStudentSubmissionRequest request, CancellationToken ct = default)
    {
        RequireRole(UserRole.Student);
        if (request.Answers is null || request.Answers.Count != 3
            || request.Answers.Any(a => a is null || a.BusinessId == Guid.Empty || a.QuestionId == Guid.Empty
                || string.IsNullOrWhiteSpace(a.AnswerText)))
            throw new BadRequestException("Phải gửi đúng 3 câu trả lời không rỗng với Business và Question hợp lệ.");
        if (request.Answers.Select(a => a.BusinessId).Distinct().Count() != 3)
            throw new BadRequestException("Phải chọn 3 Business khác nhau.");

        var student = await FindStudentAsync(ct);
        // Keep the ID stable across execution-strategy retries after an uncertain commit.
        var submissionId = Guid.NewGuid();
        var submittedAt = DateTime.UtcNow;
        return await uow.ExecuteInTransactionAsync(async token =>
        {
            await uow.StudentSubmissions.LockUserAsync(student.Id, token);
            var user = await FindStudentAsync(token);
            if (user.Id != student.Id)
                throw new ConflictException("Thông tin sinh viên đã thay đổi. Vui lòng thử lại.");
            var run = RequireRepository(user);
            var existing = await uow.StudentSubmissions.GetOwnedAsync(submissionId, user.Id, token);
            if (existing is not null)
                return new StudentSubmissionCreatedDto(existing.Id, existing.SubmittedAt, existing.Answers.Count);
            if (await uow.StudentSubmissions.ExistsAsync(user.Id, run.Id, token))
                throw new ConflictException("Sinh viên đã nộp bài cho repository này.");

            var businesses = await uow.StudentSubmissions.GetBusinessesWithQuestionsAsync(run.Id, token);
            if (businesses.Count < 3)
                throw new UnprocessableException("Repository chưa có đủ 3 Business có câu hỏi.");
            foreach (var answer in request.Answers)
            {
                var business = businesses.FirstOrDefault(b => b.Id == answer.BusinessId);
                if (business is null || !business.Questions.Any(q => q.Id == answer.QuestionId))
                    throw new BadRequestException("Business không thuộc repository hoặc Question không thuộc Business đã chọn.");
            }

            var submission = new StudentSubmission
            {
                Id = submissionId, UserId = user.Id, AnalysisRunId = run.Id, SubmittedAt = submittedAt,
                Answers = request.Answers.Select(a => new StudentAnswer
                {
                    Id = Guid.NewGuid(), UserId = user.Id, SubmissionId = submissionId,
                    QuestionId = a.QuestionId, AnswerText = a.AnswerText.Trim(), CreatedAt = submittedAt
                }).ToList()
            };
            uow.StudentSubmissions.Add(submission);
            return new StudentSubmissionCreatedDto(submission.Id, submission.SubmittedAt, submission.Answers.Count);
        }, ct);
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

    private static StudentSubmissionDetailDto MapDetail(StudentSubmission submission)
        => new(submission.Id, submission.SubmittedAt, MapRepository(submission.AnalysisRun),
            submission.Answers.OrderBy(a => a.Question.Business.BusinessName).ThenBy(a => a.QuestionId)
                .Select(a => new SubmittedAnswerDto(a.Question.BusinessId, a.Question.Business.BusinessName,
                    a.QuestionId, a.Question.QuestionText, a.AnswerText)).ToList());
}
