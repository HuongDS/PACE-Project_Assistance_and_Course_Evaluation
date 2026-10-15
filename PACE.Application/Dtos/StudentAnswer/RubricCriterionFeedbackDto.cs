using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PACE.Application.Dtos.StudentAnswer
{
    public class RubricCriterionFeedbackDto
    {
        public string Name { get; set; } = string.Empty;

        public double Score { get; set; }

        public string Comment { get; set; } = string.Empty;
    }
}
