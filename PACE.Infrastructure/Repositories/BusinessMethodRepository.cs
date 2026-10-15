using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class BusinessMethodRepository(AppDbContext context) : GenericRepository<BusinessMethod>(context), IBusinessMethodRepository
{
    public Task<List<BusinessMethod>> GetByBusinessMethodsIdWithMethodSourceAsync(Guid id)
=> DbSet.Include(m => m.MethodSource)
        .Where(b => b.BusinessId == id)
        .ToListAsync();
}
