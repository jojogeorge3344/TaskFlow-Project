namespace TaskFlow.Application.Email;

/// <summary>Sends the queued emails that are due, retrying failures and skipping stale ones.</summary>
public interface IEmailDispatcher
{
    /// <summary>Processes one batch of due emails and returns how many were sent.</summary>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);
}
