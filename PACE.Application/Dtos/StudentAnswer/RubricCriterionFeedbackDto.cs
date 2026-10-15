using System.Text.Json.Serialization;

namespace PACE.Application.Dtos.StudentAnswer
{
    public class RubricCriterionFeedbackDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; } = string.Empty;
    }
}
