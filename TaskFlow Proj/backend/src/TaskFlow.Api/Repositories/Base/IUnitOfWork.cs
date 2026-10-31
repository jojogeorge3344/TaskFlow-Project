namespace TaskFlow.Api.Repositories.Base;

/// <summary>Commits what the repositories of one request have staged. Repositories never save; the service that owns the use
/// case calls <see cref="SaveChangesAsync"/> once the change is complete, so a multi-step write is all or nothing.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Runs <paramref name="work"/> in one database transaction: committed when it returns, rolled back when it
    /// throws. The work may save several times (for example to get a generated id). A call inside an open transaction joins
    /// it.</summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);

    /// <summary>Forgets every entity this request has loaded or staged, so a failed write cannot leave half-made changes
    /// behind for the next operation, and later reads see the database as it is.</summary>
    void DetachAll();
}
