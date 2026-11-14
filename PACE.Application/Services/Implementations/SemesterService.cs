using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;

namespace PACE.Application.Services.Implementations;

public class SemesterService(IUnitOfWork uow) : ISemesterService
{
    private static SemesterDto MapToDto(Semester s, bool isCurrent)
    {
        return new SemesterDto(
            s.Id,
            s.Name,
            s.AcademicYear,
            s.StartDate,
            s.ActualStartDate,
            s.EndDate,
            isCurrent,
            s.Classes.Count);
    }

    public async Task<List<SemesterDto>> GetAllAsync(CancellationToken ct = default)
    {
        var current = await uow.Semesters.GetCurrentSemesterAsync(ct);
        var list = await uow.Semesters.GetAllOrderedAsync(ct);
        return list.Select(s => MapToDto(s, current != null && s.Id == current.Id)).ToList();
    }

    public async Task<SemesterDto?> GetCurrentSemesterAsync(CancellationToken ct = default)
    {
        var current = await uow.Semesters.GetCurrentSemesterAsync(ct);
        return current != null ? MapToDto(current, true) : null;
    }

    public async Task<SemesterDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var semester = await uow.Semesters.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Học kỳ không tồn tại.");

        var current = await uow.Semesters.GetCurrentSemesterAsync(ct);
        return MapToDto(semester, current != null && semester.Id == current.Id);
    }

    public async Task<SemesterDto> CreateAsync(CreateSemesterRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Tên học kỳ không được để trống.");

        if (string.IsNullOrWhiteSpace(request.AcademicYear))
            throw new BadRequestException("Năm học không được để trống.");

        if (request.StartDate >= request.EndDate)
            throw new BadRequestException("Ngày bắt đầu học kỳ phải trước ngày kết thúc.");

        var trimmedName = request.Name.Trim();
        var trimmedYear = request.AcademicYear.Trim();

        var exists = await uow.Semesters.AnyAsync(
            s => s.Name.ToLower() == trimmedName.ToLower() && s.AcademicYear.ToLower() == trimmedYear.ToLower(), ct);
        if (exists)
            throw new ConflictException($"Học kỳ '{trimmedName}' của năm học '{trimmedYear}' đã tồn tại.");

        var semester = new Semester
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            AcademicYear = trimmedYear,
            StartDate = request.StartDate,
            ActualStartDate = request.ActualStartDate ?? request.StartDate,
            EndDate = request.EndDate
        };

        await uow.Semesters.AddAsync(semester, ct);
        await uow.SaveChangesAsync(ct);

        var current = await uow.Semesters.GetCurrentSemesterAsync(ct);
        return MapToDto(semester, current != null && semester.Id == current.Id);
    }

    public async Task<SemesterDto> UpdateAsync(Guid id, UpdateSemesterRequest request, CancellationToken ct = default)
    {
        var semester = await uow.Semesters.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Học kỳ không tồn tại.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BadRequestException("Tên học kỳ không được để trống.");

        if (string.IsNullOrWhiteSpace(request.AcademicYear))
            throw new BadRequestException("Năm học không được để trống.");

        if (request.StartDate >= request.EndDate)
            throw new BadRequestException("Ngày bắt đầu học kỳ phải trước ngày kết thúc.");

        var trimmedName = request.Name.Trim();
        var trimmedYear = request.AcademicYear.Trim();

        var duplicate = await uow.Semesters.AnyAsync(
            s => s.Id != id && s.Name.ToLower() == trimmedName.ToLower() && s.AcademicYear.ToLower() == trimmedYear.ToLower(), ct);
        if (duplicate)
            throw new ConflictException($"Học kỳ '{trimmedName}' của năm học '{trimmedYear}' đã tồn tại.");

        semester.Name = trimmedName;
        semester.AcademicYear = trimmedYear;
        semester.StartDate = request.StartDate;
        semester.ActualStartDate = request.ActualStartDate ?? request.StartDate;
        semester.EndDate = request.EndDate;

        uow.Semesters.Update(semester);
        await uow.SaveChangesAsync(ct);

        var current = await uow.Semesters.GetCurrentSemesterAsync(ct);
        return MapToDto(semester, current != null && semester.Id == current.Id);
    }
}

