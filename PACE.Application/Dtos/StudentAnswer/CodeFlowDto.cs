using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PACE.Application.Dtos.StudentAnswer
{
    public class CodeFlowDto
    {
        public BusinessDto? Business { get; set; }
        public List<MethodSourceDto> Methods { get; set; } = new();
    }
}
