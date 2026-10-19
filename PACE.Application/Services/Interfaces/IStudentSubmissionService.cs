using PACE.Application.Dtos;
using PACE.Application.Common.Models;

namespace PACE.Application.Services.Interfaces;

public interface IStudentSubmissionService
{
    Task<StudentSubmissionJoinedDto> JoinAsync(JoinStudentSubmissionRequest request, CancellationToken ct = default);
    Task<StudentSubmissionLookupDto> LookupAsync(CancellationToken ct = default);
    Task<StudentSubmissionResultDto> SubmitAsync(SubmitStudentSubmissionRequest request, CancellationToken ct = default);
    Task<StudentSubmissionDetailDto> RetrieveAsync(RetrieveStudentSubmissionRequest request, CancellationToken ct = default);
    Task<StudentSubmissionDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<StudentSubmissionDetailDto>> GetMineAsync(StudentSubmissionQuery query, CancellationToken ct = default);
    Task<PagedResult<AdminStudentSubmissionDto>> GetAllAsync(StudentSubmissionQuery query, CancellationToken ct = default);
    Task<AdminStudentSubmissionDto> QuickCreateAsync(QuickCreateStudentSubmissionRequest request, CancellationToken ct = default);
}
