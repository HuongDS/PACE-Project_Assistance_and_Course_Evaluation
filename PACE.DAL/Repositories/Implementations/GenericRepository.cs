using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PACE.DAL.Data;
using PACE.DAL.Repositories.Interfaces;

namespace PACE.DAL.Repositories.Implementations;

public class GenericRepository<T>(AppDbContext context) : IGenericRepository<T> where T : class
{
    protected readonly AppDbContext Context = context;
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet.FindAsync([id], ct);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, bool asNoTracking = false, CancellationToken ct = default)
        => Source(asNoTracking).FirstOrDefaultAsync(predicate, ct);

    public Task<List<T>> FindAsync(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true, CancellationToken ct = default)
        => (predicate is null ? Source(asNoTracking) : Source(asNoTracking).Where(predicate)).ToListAsync(ct);

    public Task<PagedResult<T>> GetPagedAsync(int page, int pageSize, Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => ToPagedAsync(predicate is null ? DbSet.AsNoTracking() : DbSet.AsNoTracking().Where(predicate), page, pageSize, ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => DbSet.AnyAsync(predicate, ct);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null ? DbSet.CountAsync(ct) : DbSet.CountAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await DbSet.AddAsync(entity, ct);

    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        => DbSet.AddRangeAsync(entities, ct);

    public void Update(T entity) => DbSet.Update(entity);

    public void Remove(T entity) => DbSet.Remove(entity);

    public void RemoveRange(IEnumerable<T> entities) => DbSet.RemoveRange(entities);

    protected IQueryable<T> Source(bool asNoTracking) => asNoTracking ? DbSet.AsNoTracking() : DbSet;

    protected static async Task<PagedResult<TItem>> ToPagedAsync<TItem>(IQueryable<TItem> query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<TItem> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    protected static string? NormalizeSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
}
