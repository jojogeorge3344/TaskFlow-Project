namespace TaskFlow.Api.Repositories.Interfaces;

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
}
