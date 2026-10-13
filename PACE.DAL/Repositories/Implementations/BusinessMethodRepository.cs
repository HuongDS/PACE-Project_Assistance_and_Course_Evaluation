using PACE.DAL.Data;
using PACE.DAL.Models;
using PACE.DAL.Repositories.Interfaces;

namespace PACE.DAL.Repositories.Implementations;

public class BusinessMethodRepository(AppDbContext context) : GenericRepository<BusinessMethod>(context), IBusinessMethodRepository
{
}
