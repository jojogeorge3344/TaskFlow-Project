using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces;

namespace TaskFlow.Api.Common;

/// <summary>The shared part of every entity repository. Module repositories derive from it and add queries named for what
/// they find; they use <see cref="Set"/> and <see cref="Db"/> internally and never hand a query to their caller.</summary>
public abstract class BaseRepository<TEntity> : IBaseRepository<TEntity>
    where TEntity : class
{
    protected BaseRepository(AppDbContext db)
    {
        Db = db;
    }

    protected AppDbContext Db { get; }

    protected DbSet<TEntity> Set => Db.Set<TEntity>();

    public async Task<TEntity?> FindAsync(object id, CancellationToken cancellationToken = default) =>
        await Set.FindAsync([id], cancellationToken);

    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(predicate, cancellationToken);

    public void Add(TEntity entity) => Set.Add(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);
}
