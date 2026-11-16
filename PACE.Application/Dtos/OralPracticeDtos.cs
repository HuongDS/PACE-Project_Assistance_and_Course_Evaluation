using System.ComponentModel.DataAnnotations;
using PACE.Application.Common.Models;

namespace PACE.Application.Dtos;

// OralExam DTOs
public record OralExamDto(
    Guid Id,
    Guid ClassId,
    string? ClassName,
    string Title,
    string? Description,
    DateTime CreatedAt,
    int QuestionCount,
    int SessionCount);

public record OralExamDetailDto(
    Guid Id,
    Guid ClassId,
    string? ClassName,
    string Title,
    string? Description,
    DateTime CreatedAt,
    IReadOnlyList<OralQuestionDto> Questions);

public class OralExamQuery : PageQuery
{
    public Guid? ClassId { get; set; }
    public string? SearchTerm { get; set; }
}

public class CreateOralExamRequest
{
    [Required(ErrorMessage = "ClassId không được để trống.")]
    public Guid ClassId { get; set; }

    [Required(ErrorMessage = "Tiêu đề kỳ vấn đáp không được để trống.")]
    [MaxLength(255, ErrorMessage = "Tiêu đề tối đa 255 ký tự.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }
}

public class UpdateOralExamRequest
{
    [Required(ErrorMessage = "Tiêu đề kỳ vấn đáp không được để trống.")]
    [MaxLength(255, ErrorMessage = "Tiêu đề tối đa 255 ký tự.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }
}

// OralQuestion DTOs
public record OralQuestionDto(
    Guid Id,
    Guid OralExamId,
    Guid? GeneratedQuestionId,
    string Content,
    string? ReferenceContext,
    string? ExpectedKeywords);

public class OralQuestionQuery : PageQuery
{
    public Guid? OralExamId { get; set; }
    public string? SearchTerm { get; set; }
}

public class CreateOralQuestionRequest
{
    [Required(ErrorMessage = "OralExamId không được để trống.")]
    public Guid OralExamId { get; set; }

    public Guid? GeneratedQuestionId { get; set; }

    [Required(ErrorMessage = "Nội dung câu hỏi không được để trống.")]
    public string Content { get; set; } = null!;

    public string? ReferenceContext { get; set; }
    public string? ExpectedKeywords { get; set; }
}

public class UpdateOralQuestionRequest
{
    [Required(ErrorMessage = "Nội dung câu hỏi không được để trống.")]
    public string Content { get; set; } = null!;

    public string? ReferenceContext { get; set; }
    public string? ExpectedKeywords { get; set; }
}

// StudentOralSession DTOs
public record StudentOralAnswerDto(
    Guid Id,
    Guid SessionId,
    Guid QuestionId,
    string? QuestionContent,
    string? ExpectedKeywords,
    string? StudentAnswer,
    string? Feedback);

public record StudentOralSessionDto(
    Guid Id,
    Guid OralExamId,
    string? OralExamTitle,
    string StudentCode,
    string? StudentName,
    string? ExaminerCode,
    string? ExaminerName,
    DateTime StartTime,
    DateTime? EndTime,
    string? AudioRecordUrl,
    string? ScreenRecordUrl,
    string? AiEvaluationSummary,
    string? FinalFeedback,
    int AnswerCount);

public record StudentOralSessionDetailDto(
    Guid Id,
    Guid OralExamId,
    string? OralExamTitle,
    string StudentCode,
    string? StudentName,
    string? ExaminerCode,
    string? ExaminerName,
    DateTime StartTime,
    DateTime? EndTime,
    string? AudioRecordUrl,
    string? ScreenRecordUrl,
    string? AiEvaluationSummary,
    string? FinalFeedback,
    IReadOnlyList<StudentOralAnswerDto> Answers);

public class StudentOralSessionQuery : PageQuery
{
    public Guid? OralExamId { get; set; }
    public string? StudentCode { get; set; }
    public string? ExaminerCode { get; set; }
}

public class StartStudentOralSessionRequest
{
    [Required(ErrorMessage = "OralExamId không được để trống.")]
    public Guid OralExamId { get; set; }

    [Required(ErrorMessage = "Mã sinh viên không được để trống.")]
    [MaxLength(20, ErrorMessage = "Mã sinh viên tối đa 20 ký tự.")]
    public string StudentCode { get; set; } = null!;

    [MaxLength(20, ErrorMessage = "Mã giám khảo tối đa 20 ký tự.")]
    public string? ExaminerCode { get; set; }

    public DateTime? StartTime { get; set; }
}

public class UpdateStudentOralSessionRequest
{
    public DateTime? EndTime { get; set; }
    public string? AudioRecordUrl { get; set; }
    public string? ScreenRecordUrl { get; set; }
    public string? AiEvaluationSummary { get; set; }
    public string? FinalFeedback { get; set; }
}

// StudentOralAnswer Requests
public class SubmitOralAnswerRequest
{
    [Required(ErrorMessage = "QuestionId không được để trống.")]
    public Guid QuestionId { get; set; }

    public string? StudentAnswer { get; set; }
    public string? Feedback { get; set; }
}

public class UpdateOralAnswerRequest
{
    public string? StudentAnswer { get; set; }
    public string? Feedback { get; set; }
}
