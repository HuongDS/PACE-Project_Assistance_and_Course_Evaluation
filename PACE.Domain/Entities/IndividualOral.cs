namespace PACE.Domain.Entities;

/// <summary>Cấu hình của phiên vấn đáp riêng từng sinh viên: thời gian mỗi lượt và bản mã nguồn dùng để hỏi.</summary>
public class IndividualOralSetup
{
    public Guid SessionId { get; set; }
    public AssessmentSession Session { get; set; } = null!;
    public int DurationMinutes { get; set; } = 20;
    public int QuestionsPerStudent { get; set; } = 4;
    public string Difficulty { get; set; } = "Trung bình";
    /// <summary>Chỉ tính commit đến mốc này.</summary>
    public DateTime? CodeDeadline { get; set; }
    public string? CommitSha { get; set; }
    public DateTime? CommitAt { get; set; }
    public DateTime? ScannedAt { get; set; }
    /// <summary>Hàm đã đọc được tại commit đó, số dòng của từng tác giả và tác giả nào là sinh viên nào.</summary>
    public string? SnapshotJson { get; set; }
}

/// <summary>Đề và lượt thi của một sinh viên trong phiên.</summary>
public class OralAttempt
{
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public SessionStudent Student { get; set; } = null!;
    public string QuestionsJson { get; set; } = "[]";
    /// <summary>Giảng viên đã xem và chốt đề.</summary>
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string AnswersJson { get; set; } = "[]";
    /// <summary>Nhận xét gợi ý của AI theo rubric. Không chứa điểm.</summary>
    public string? AiReviewJson { get; set; }
    public DateTime? AiReviewedAt { get; set; }
    /// <summary>Các lượt cũ khi giảng viên mở lại.</summary>
    public string? HistoryJson { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
