using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.Mappings;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Services.Implementations;

public class RubricCriterionService(IUnitOfWork uow) : IRubricCriterionService
{
    public async Task<IReadOnlyList<RubricCriterionDto>> GetAllAsync(CancellationToken ct = default)
        => (await uow.RubricCriteria.GetAllOrderedAsync(ct)).Select(c => c.ToDto()).ToList();

    public async Task<RubricCriterionDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        => (await uow.RubricCriteria.GetByIdAsync(id, ct) ?? throw new NotFoundException("tiêu chí", id)).ToDto();

    public async Task<RubricCriterionDto> CreateAsync(UpsertRubricCriterionRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureCodeAvailableAsync(code, null, ct);

        var criterion = new RubricCriterion
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Statement = request.Statement.Trim(),
            DisplayOrder = request.DisplayOrder
        };
        await uow.RubricCriteria.AddAsync(criterion, ct);
        await uow.SaveChangesAsync(ct);
        return criterion.ToDto();
    }

    public async Task<RubricCriterionDto> UpdateAsync(Guid id, UpsertRubricCriterionRequest request, CancellationToken ct = default)
    {
        var criterion = await uow.RubricCriteria.GetByIdAsync(id, ct) ?? throw new NotFoundException("tiêu chí", id);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureCodeAvailableAsync(code, id, ct);

        criterion.Code = code;
        criterion.Name = request.Name.Trim();
        criterion.Statement = request.Statement.Trim();
        criterion.DisplayOrder = request.DisplayOrder;
        await uow.SaveChangesAsync(ct);
        return criterion.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var criterion = await uow.RubricCriteria.GetByIdAsync(id, ct) ?? throw new NotFoundException("tiêu chí", id);
        if (await uow.RubricCriteria.IsUsedAsync(id, ct))
            throw new ConflictException("Tiêu chí đã có sinh viên chấm điểm, không thể xoá.");
        uow.RubricCriteria.Remove(criterion);
        await uow.SaveChangesAsync(ct);
    }

    private async Task EnsureCodeAvailableAsync(string code, Guid? exceptId, CancellationToken ct)
    {
        if (await uow.RubricCriteria.CodeExistsAsync(code, exceptId, ct))
            throw new ConflictException($"Mã tiêu chí {code} đã tồn tại.");
    }
}
