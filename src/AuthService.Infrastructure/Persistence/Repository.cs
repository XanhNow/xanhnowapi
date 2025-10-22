using AuthService.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AuthService.Infrastructure.Persistence;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T> _set;
    public Repository(AppDbContext db) { _db = db; _set = db.Set<T>(); }

    public Task<T?> GetByIdAsync(object id, CancellationToken ct = default) => _set.FindAsync(new[] { id }, ct).AsTask();
    public async Task AddAsync(T entity, CancellationToken ct = default) => await _set.AddAsync(entity, ct);
    public void Update(T entity) => _set.Update(entity);
    public void Remove(T entity) => _set.Remove(entity);
    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => _set.FirstOrDefaultAsync(p, ct);
    public IQueryable<T> Query(Expression<Func<T, bool>>? p = null) => p == null ? _set.AsQueryable() : _set.Where(p);
}
