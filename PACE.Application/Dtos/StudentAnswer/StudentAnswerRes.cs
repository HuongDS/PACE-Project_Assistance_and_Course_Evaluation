using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos.StudentAnswer
{
    public class StudentAnswerRes
    {
        [JsonIgnore]
        public bool HasContent => new[] { Positives, ConstructiveCriticism, NextSteps, CodeEvidence, LogicExplanation, Comment }
            .Any(value => !string.IsNullOrWhiteSpace(value))
            || Criteria?.Any(c => !string.IsNullOrWhiteSpace(c.Name) || !string.IsNullOrWhiteSpace(c.Comment)) == true
            || Researching != null;

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

        // Researching format (Multi-LLM Ensemble)
        [JsonPropertyName("researching")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ResearchingFeedbackDto? Researching { get; set; }

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

    public class ResearchingFeedbackDto
    {
        [JsonPropertyName("consensus_summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ConsensusSummary { get; set; }

        [JsonPropertyName("code_inspection")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CodeInspection { get; set; }

        [JsonPropertyName("pedagogical_guidance")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PedagogicalGuidance { get; set; }

        [JsonPropertyName("models_involved")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? ModelsInvolved { get; set; }
    }
}
