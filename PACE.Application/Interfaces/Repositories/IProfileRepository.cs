using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Domain.Entities;

namespace PACE.Application.Interfaces.Repositories;

public interface IProfileRepository
{
    // StudentProfile
    Task<PagedResult<StudentProfileDto>> SearchStudentProfilesAsync(StudentProfileQuery query, CancellationToken ct = default);
    Task<StudentProfile?> GetStudentProfileByCodeAsync(string studentCode, CancellationToken ct = default);
    Task<StudentProfile?> GetStudentProfileByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> StudentCodeExistsAsync(string studentCode, CancellationToken ct = default);
    Task AddStudentProfileAsync(StudentProfile profile, CancellationToken ct = default);
    void UpdateStudentProfile(StudentProfile profile);
    void RemoveStudentProfile(StudentProfile profile);

    // LecturerProfile
    Task<PagedResult<LecturerProfileDto>> SearchLecturerProfilesAsync(LecturerProfileQuery query, CancellationToken ct = default);
    Task<LecturerProfile?> GetLecturerProfileByCodeAsync(string lecturerCode, CancellationToken ct = default);
    Task<LecturerProfile?> GetLecturerProfileByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> LecturerCodeExistsAsync(string lecturerCode, CancellationToken ct = default);
    Task AddLecturerProfileAsync(LecturerProfile profile, CancellationToken ct = default);
    void UpdateLecturerProfile(LecturerProfile profile);
    void RemoveLecturerProfile(LecturerProfile profile);
}
