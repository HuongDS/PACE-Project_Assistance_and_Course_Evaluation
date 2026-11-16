using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;

namespace PACE.Application.Services.Implementations;

public class ProfileService(
    IUnitOfWork uow,
    ICurrentUserService currentUser) : IProfileService
{
    private void EnsureAdmin()
    {
        if (!currentUser.IsAdmin)
            throw new ForbiddenException("Chỉ quản trị viên mới có quyền thực hiện thao tác trên hồ sơ người dùng.");
    }

    // StudentProfile
    public async Task<PagedResult<StudentProfileDto>> GetStudentProfilesPagedAsync(StudentProfileQuery query, CancellationToken ct = default)
    {
        return await uow.Profiles.SearchStudentProfilesAsync(query, ct);
    }

    public async Task<StudentProfileDto> GetStudentProfileByCodeAsync(string studentCode, CancellationToken ct = default)
    {
        var profile = await uow.Profiles.GetStudentProfileByCodeAsync(studentCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ sinh viên với mã '{studentCode}'.");

        return new StudentProfileDto(
            profile.StudentCode,
            profile.UserId,
            profile.User?.FullName,
            profile.User?.Email,
            profile.EnrollmentYear,
            profile.CreatedAt);
    }

    public async Task<StudentProfileDto> CreateStudentProfileAsync(CreateStudentProfileRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();

        var trimmedCode = request.StudentCode.Trim().ToUpperInvariant();
        var exists = await uow.Profiles.StudentCodeExistsAsync(trimmedCode, ct);
        if (exists)
            throw new ConflictException($"Mã sinh viên '{trimmedCode}' đã tồn tại trong hệ thống.");

        var user = await uow.Users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("Tài khoản người dùng (User) không tồn tại.");

        var existingUserProfile = await uow.Profiles.GetStudentProfileByUserIdAsync(request.UserId, ct);
        if (existingUserProfile != null)
            throw new ConflictException("Tài khoản này đã được liên kết với một hồ sơ sinh viên.");

        var profile = new StudentProfile
        {
            StudentCode = trimmedCode,
            UserId = request.UserId,
            EnrollmentYear = request.EnrollmentYear,
            CreatedAt = DateTime.UtcNow
        };

        await uow.Profiles.AddStudentProfileAsync(profile, ct);
        await uow.SaveChangesAsync(ct);

        return new StudentProfileDto(
            profile.StudentCode,
            profile.UserId,
            user.FullName,
            user.Email,
            profile.EnrollmentYear,
            profile.CreatedAt);
    }

    public async Task<StudentProfileDto> UpdateStudentProfileAsync(string studentCode, UpdateStudentProfileRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();

        var profile = await uow.Profiles.GetStudentProfileByCodeAsync(studentCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ sinh viên với mã '{studentCode}'.");

        profile.EnrollmentYear = request.EnrollmentYear;

        uow.Profiles.UpdateStudentProfile(profile);
        await uow.SaveChangesAsync(ct);

        return new StudentProfileDto(
            profile.StudentCode,
            profile.UserId,
            profile.User?.FullName,
            profile.User?.Email,
            profile.EnrollmentYear,
            profile.CreatedAt);
    }

    public async Task DeleteStudentProfileAsync(string studentCode, CancellationToken ct = default)
    {
        EnsureAdmin();

        var profile = await uow.Profiles.GetStudentProfileByCodeAsync(studentCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ sinh viên với mã '{studentCode}'.");

        uow.Profiles.RemoveStudentProfile(profile);
        await uow.SaveChangesAsync(ct);
    }

    // LecturerProfile
    public async Task<PagedResult<LecturerProfileDto>> GetLecturerProfilesPagedAsync(LecturerProfileQuery query, CancellationToken ct = default)
    {
        return await uow.Profiles.SearchLecturerProfilesAsync(query, ct);
    }

    public async Task<LecturerProfileDto> GetLecturerProfileByCodeAsync(string lecturerCode, CancellationToken ct = default)
    {
        var profile = await uow.Profiles.GetLecturerProfileByCodeAsync(lecturerCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ giảng viên với mã '{lecturerCode}'.");

        return new LecturerProfileDto(
            profile.LecturerCode,
            profile.UserId,
            profile.User?.FullName,
            profile.User?.Email,
            profile.AcademicTitle,
            profile.CreatedAt);
    }

    public async Task<LecturerProfileDto> CreateLecturerProfileAsync(CreateLecturerProfileRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();

        var trimmedCode = request.LecturerCode.Trim().ToUpperInvariant();
        var exists = await uow.Profiles.LecturerCodeExistsAsync(trimmedCode, ct);
        if (exists)
            throw new ConflictException($"Mã giảng viên '{trimmedCode}' đã tồn tại trong hệ thống.");

        var user = await uow.Users.GetByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException("Tài khoản người dùng (User) không tồn tại.");

        var existingUserProfile = await uow.Profiles.GetLecturerProfileByUserIdAsync(request.UserId, ct);
        if (existingUserProfile != null)
            throw new ConflictException("Tài khoản này đã được liên kết với một hồ sơ giảng viên.");

        var profile = new LecturerProfile
        {
            LecturerCode = trimmedCode,
            UserId = request.UserId,
            AcademicTitle = request.AcademicTitle.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await uow.Profiles.AddLecturerProfileAsync(profile, ct);
        await uow.SaveChangesAsync(ct);

        return new LecturerProfileDto(
            profile.LecturerCode,
            profile.UserId,
            user.FullName,
            user.Email,
            profile.AcademicTitle,
            profile.CreatedAt);
    }

    public async Task<LecturerProfileDto> UpdateLecturerProfileAsync(string lecturerCode, UpdateLecturerProfileRequest request, CancellationToken ct = default)
    {
        EnsureAdmin();

        var profile = await uow.Profiles.GetLecturerProfileByCodeAsync(lecturerCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ giảng viên với mã '{lecturerCode}'.");

        profile.AcademicTitle = request.AcademicTitle.Trim();

        uow.Profiles.UpdateLecturerProfile(profile);
        await uow.SaveChangesAsync(ct);

        return new LecturerProfileDto(
            profile.LecturerCode,
            profile.UserId,
            profile.User?.FullName,
            profile.User?.Email,
            profile.AcademicTitle,
            profile.CreatedAt);
    }

    public async Task DeleteLecturerProfileAsync(string lecturerCode, CancellationToken ct = default)
    {
        EnsureAdmin();

        var profile = await uow.Profiles.GetLecturerProfileByCodeAsync(lecturerCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ giảng viên với mã '{lecturerCode}'.");

        uow.Profiles.RemoveLecturerProfile(profile);
        await uow.SaveChangesAsync(ct);
    }
}
