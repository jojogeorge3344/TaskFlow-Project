using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Repositories.Interfaces;

namespace TaskFlow.Api.Repositories;

/// <summary>The unit of work over the request's <see cref="AppDbContext"/>. Every repository in the request shares that
/// context, so one save commits everything they staged.</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Saves, turning the database's race and unique-index failures into <see cref="ConcurrencyConflictException"/>
    /// and <see cref="UniqueConstraintException"/>.</summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new UniqueConstraintException(exception);
        }
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            async token =>
            {
                await work(token);
                return true;
            },
            cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            return await work(cancellationToken);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
