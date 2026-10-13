using PACE.DAL.Models;

namespace PACE.DAL.Repositories.Interfaces;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetWithRepoAsync(Guid id, bool asNoTracking = true, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, Guid? exceptId = null, CancellationToken ct = default);
    Task<PagedResult<User>> SearchAsync(UserRole? role, Guid? analysisRunId, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<HashSet<string>> GetAllEmailsAsync(CancellationToken ct = default);
    Task<List<string>> GetEmailsByRepoAsync(Guid analysisRunId, CancellationToken ct = default);
}
