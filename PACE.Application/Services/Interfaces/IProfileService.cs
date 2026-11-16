using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IProfileService
{
    // StudentProfile
    Task<PagedResult<StudentProfileDto>> GetStudentProfilesPagedAsync(StudentProfileQuery query, CancellationToken ct = default);
    Task<StudentProfileDto> GetStudentProfileByCodeAsync(string studentCode, CancellationToken ct = default);
    Task<StudentProfileDto> CreateStudentProfileAsync(CreateStudentProfileRequest request, CancellationToken ct = default);
    Task<StudentProfileDto> UpdateStudentProfileAsync(string studentCode, UpdateStudentProfileRequest request, CancellationToken ct = default);
    Task DeleteStudentProfileAsync(string studentCode, CancellationToken ct = default);

    // LecturerProfile
    Task<PagedResult<LecturerProfileDto>> GetLecturerProfilesPagedAsync(LecturerProfileQuery query, CancellationToken ct = default);
    Task<LecturerProfileDto> GetLecturerProfileByCodeAsync(string lecturerCode, CancellationToken ct = default);
    Task<LecturerProfileDto> CreateLecturerProfileAsync(CreateLecturerProfileRequest request, CancellationToken ct = default);
    Task<LecturerProfileDto> UpdateLecturerProfileAsync(string lecturerCode, UpdateLecturerProfileRequest request, CancellationToken ct = default);
    Task DeleteLecturerProfileAsync(string lecturerCode, CancellationToken ct = default);
}
