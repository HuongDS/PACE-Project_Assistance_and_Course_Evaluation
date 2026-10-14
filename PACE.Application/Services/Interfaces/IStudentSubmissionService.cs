using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IStudentSubmissionService
{
    Task<StudentSubmissionLookupDto> LookupAsync(StudentIdentityRequest request, CancellationToken ct = default);
    Task<StudentSubmissionCreatedDto> SubmitAsync(SubmitStudentSubmissionRequest request, CancellationToken ct = default);
    Task<StudentSubmissionDetailDto> RetrieveAsync(RetrieveStudentSubmissionRequest request, CancellationToken ct = default);
}
