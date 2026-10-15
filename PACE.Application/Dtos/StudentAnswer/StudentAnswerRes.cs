using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos.StudentAnswer
{
    public class StudentAnswerRes
    {
        [JsonPropertyName("student_answer_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? StudentAnswerId { get; set; }

        [JsonPropertyName("feedback_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FeedbackFormat FeedbackType { get; set; } = FeedbackFormat.Sandwich;

        [JsonPropertyName("comment")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Comment { get; set; }

        // Sandwich format
        [JsonPropertyName("positives")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Positives { get; set; }

        [JsonPropertyName("constructive_criticism")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ConstructiveCriticism { get; set; }

        [JsonPropertyName("next_steps")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NextSteps { get; set; }

        // Rubric format
        [JsonPropertyName("criteria")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<RubricCriterionFeedbackDto>? Criteria { get; set; }

        // Explainable format
        [JsonPropertyName("code_evidence")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CodeEvidence { get; set; }

        [JsonPropertyName("logic_explanation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LogicExplanation { get; set; }

        // Guardrails Metadata
        [JsonPropertyName("guardrail_warnings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? GuardrailWarnings { get; set; }

        [JsonPropertyName("requires_human_review")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RequiresHumanReview { get; set; } = false;

        [JsonPropertyName("review_reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReviewReason { get; set; }
    }
}
