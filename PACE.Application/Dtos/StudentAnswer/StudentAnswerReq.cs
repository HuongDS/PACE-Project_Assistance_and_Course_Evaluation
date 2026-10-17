using System;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos.StudentAnswer
{
    public class StudentAnswerReq
    {
        public Guid BusinessId { get; set; }
        public Guid QuestionId { get; set; }
        public Guid? SubmissionId { get; set; }
        public Guid? UserId { get; set; }
        public string Question { get; set; } = string.Empty;
        //public string? ReferenceMaterial { get; set; }
        public string StudentAnswer { get; set; } = string.Empty;
        public FeedbackFormat FeedbackFormat { get; set; } = FeedbackFormat.Sandwich;
        public ContextType ContextModel { get; set; } = ContextType.Raw;
    }
}
