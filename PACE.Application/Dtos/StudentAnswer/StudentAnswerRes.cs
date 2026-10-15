using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos.StudentAnswer
{
    public class StudentAnswerRes
    {
        [JsonPropertyName("student_answer_id")]
        public Guid? StudentAnswerId { get; set; }

        [JsonPropertyName("feedback_type")]
        public FeedbackFormat FeedbackType { get; set; } = FeedbackFormat.Sandwich;

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        // Sandwich format
        [JsonPropertyName("positives")]
        public string? Positives { get; set; }

        [JsonPropertyName("constructive_criticism")]
        public string? ConstructiveCriticism { get; set; }

        [JsonPropertyName("next_steps")]
        public string? NextSteps { get; set; }

        // Rubric format
        [JsonPropertyName("criteria")]
        public List<RubricCriterionFeedbackDto>? Criteria { get; set; }

        // Explainable format
        [JsonPropertyName("code_evidence")]
        public string? CodeEvidence { get; set; }

        [JsonPropertyName("logic_explanation")]
        public string? LogicExplanation { get; set; }

        // Guardrails Metadata
        [JsonPropertyName("guardrail_warnings")]
        public List<string>? GuardrailWarnings { get; set; }

        [JsonPropertyName("requires_human_review")]
        public bool RequiresHumanReview { get; set; } = false;

        [JsonPropertyName("review_reason")]
        public string? ReviewReason { get; set; }
    }
}