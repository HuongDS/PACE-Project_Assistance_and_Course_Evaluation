using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PACE.Application.Dtos.StudentAnswer;

namespace PACE.Application.Services.Interfaces
{
    public interface IStudentAnswerService
    {
        Task<string> EvaluateStudentAnswerAsync(StudentAnswerReq request);
    }
}
