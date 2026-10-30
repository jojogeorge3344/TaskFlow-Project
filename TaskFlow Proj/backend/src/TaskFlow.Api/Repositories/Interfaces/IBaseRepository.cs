using System.Linq.Expressions;

namespace TaskFlow.Api.Repositories.Interfaces;

/// <summary>The data access every entity repository shares. Changes are staged only; <see cref="IUnitOfWork"/> commits them.
/// No member returns a deferred query, so callers never compose database queries themselves.</summary>
public interface IBaseRepository<TEntity>
    where TEntity : class
{
    /// <summary>The tracked entity with this key, or null.</summary>
    Task<TEntity?> FindAsync(object id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    void Add(TEntity entity);

    void Remove(TEntity entity);
}
