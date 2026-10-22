using System.Text.Json;
using System.Globalization;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;

namespace PACE.Application.Services.Implementations;

public static class SurveyResultFiltering
{
    public static void Validate(SurveyResultQuery q)
    {
        if (q.MinScore > q.MaxScore || q.MinAverageScore > q.MaxAverageScore || q.MinAiScore > q.MaxAiScore || q.FromDate > q.ToDate)
            throw new BadRequestException("Khoảng lọc không hợp lệ: giá trị từ phải nhỏ hơn hoặc bằng giá trị đến.");
    }

    private static bool Contains(string? text, string? term)
        => string.IsNullOrWhiteSpace(term) || (text?.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

    public static bool Matches(SurveySubmissionItemDto item, SurveyResultQuery q)
    {
        var scores = item.Scores;
        if (q.AnalysisRunId.HasValue && item.AnalysisRunId != q.AnalysisRunId) return false;
        if (q.BusinessId.HasValue && item.BusinessId != q.BusinessId) return false;
        if (q.QuestionId.HasValue && item.QuestionId != q.QuestionId) return false;
        if (q.ExcludeTestData && (item.StudentCode?.StartsWith("K6", StringComparison.OrdinalIgnoreCase) == true
            || item.StudentEmail.StartsWith("pace-k6-", StringComparison.OrdinalIgnoreCase))) return false;
        if (!Contains(item.StudentCode, q.StudentCode) || !Contains(item.StudentEmail, q.StudentEmail)
            || !Contains(item.RepoName, q.RepoName) || !Contains(item.BusinessName, q.BusinessName)
            || !Contains(item.QuestionText, q.QuestionText) || !Contains(item.AnswerText, q.AnswerText)
            || !Contains(item.AiFeedback, q.AiFeedback) || !Contains(item.StudentReply, q.StudentReply)) return false;
        if (!string.IsNullOrWhiteSpace(q.Search) && !new[] {item.StudentEmail, item.StudentCode, item.RepoName,
            item.BusinessName, item.QuestionText, item.AnswerText, item.AiFeedback, item.StudentReply}.Any(x => Contains(x, q.Search))) return false;
        if (!string.IsNullOrWhiteSpace(q.FeedbackFormat)
            && !string.Equals(item.FeedbackFormat, q.FeedbackFormat, StringComparison.OrdinalIgnoreCase)) return false;
        if (q.HasSurvey.HasValue && (scores.Count > 0) != q.HasSurvey) return false;
        if (q.HasFeedback.HasValue && !string.IsNullOrWhiteSpace(item.AiFeedback) != q.HasFeedback) return false;
        if (q.CriterionId.HasValue && scores.Count == 0) return false;
        if ((q.MinScore.HasValue || q.MaxScore.HasValue)
            && !scores.Any(s => (!q.MinScore.HasValue || s.Score >= q.MinScore) && (!q.MaxScore.HasValue || s.Score <= q.MaxScore))) return false;
        if ((q.MinAverageScore.HasValue || q.MaxAverageScore.HasValue) && scores.Count == 0) return false;
        if (q.MinAverageScore.HasValue && item.AverageScore < q.MinAverageScore) return false;
        if (q.MaxAverageScore.HasValue && item.AverageScore > q.MaxAverageScore) return false;
        var date = item.SubmittedAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(item.SubmittedAt.Value, DateTimeKind.Utc)) : (DateTimeOffset?)null;
        if (q.FromDate.HasValue && (!date.HasValue || date < q.FromDate)) return false;
        if (q.ToDate.HasValue && (!date.HasValue || date > q.ToDate)) return false;
        if ((!string.IsNullOrWhiteSpace(q.AiCriterionName) || q.MinAiScore.HasValue || q.MaxAiScore.HasValue)
            && !MatchesAiCriterion(item.AiFeedback, q)) return false;
        return true;
    }

    private static bool MatchesAiCriterion(string? raw, SurveyResultQuery query)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var criteria = root.EnumerateObject().FirstOrDefault(p => p.Name.Equals("criteria", StringComparison.OrdinalIgnoreCase)).Value;
            if (criteria.ValueKind != JsonValueKind.Array) return false;
            foreach (var c in criteria.EnumerateArray())
            {
                if (c.ValueKind != JsonValueKind.Object) continue;
                var fields = c.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
                var name = fields.GetValueOrDefault("name");
                if (!Contains(name.ValueKind == JsonValueKind.String ? name.GetString() : null, query.AiCriterionName)) continue;
                if (!query.MinAiScore.HasValue && !query.MaxAiScore.HasValue) return true;
                if (!double.TryParse(fields.GetValueOrDefault("score").ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var score)
                    || !double.IsFinite(score)) continue;
                if ((!query.MinAiScore.HasValue || score >= query.MinAiScore) && (!query.MaxAiScore.HasValue || score <= query.MaxAiScore)) return true;
            }
        }
        catch (JsonException) { }
        catch (ArgumentException) { }
        return false;
    }
}
