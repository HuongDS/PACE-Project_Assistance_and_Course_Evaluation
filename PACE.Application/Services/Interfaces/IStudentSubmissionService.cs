using PACE.Application.Dtos;
using PACE.Application.Common.Models;

namespace PACE.Application.Services.Interfaces;

public interface IStudentSubmissionService
{
    Task<StudentSubmissionLookupDto> LookupAsync(CancellationToken ct = default);
    Task<StudentSubmissionCreatedDto> SubmitAsync(SubmitStudentSubmissionRequest request, CancellationToken ct = default);
    Task<StudentSubmissionDetailDto> RetrieveAsync(RetrieveStudentSubmissionRequest request, CancellationToken ct = default);
    Task<PagedResult<AdminStudentSubmissionDto>> GetAllAsync(StudentSubmissionQuery query, CancellationToken ct = default);
}
