using System;
using System.Collections.Generic;

namespace PACE.Application.Dtos;

public class ManagerStatisticsDto
{
    public int TotalSubmissions { get; set; }
    public int TotalSuccessfulSubmissions { get; set; }
    public int TotalPendingEvaluationSubmissions { get; set; }
    public int TotalSurveyedStudents { get; set; }
    public List<DailySubmissionStatDto> DailySuccessfulSubmissions { get; set; } = [];
    public List<PendingEvaluationDto> PendingEvaluationSubmissions { get; set; } = [];
}

public class DailySubmissionStatDto
{
    public string Date { get; set; } = string.Empty;
    public int SuccessfulSubmissionsCount { get; set; }
}

public class PendingEvaluationDto
{
    public Guid SubmissionId { get; set; }
    public Guid UserId { get; set; }
    public string? StudentCode { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? RepoName { get; set; }
    public string? RepoUrl { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int TotalQuestions { get; set; }
    public int SurveyCompletedQuestions { get; set; }
}
