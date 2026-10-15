using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class GeneratedQuestionRepository(AppDbContext context) : GenericRepository<GeneratedQuestion>(context), IGeneratedQuestionRepository
{
}