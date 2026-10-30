using TaskFlow.Api.Common.Exceptions;

namespace TaskFlow.Api.Integrations.Interfaces.Storage;

/// <summary>Where attachment bytes live. Files are written once under a generated key and never changed.</summary>
public interface IMailFileStore
{
    /// <summary>Writes the stream under a new key; returns the key and the number of bytes written. Throws
    /// <see cref="MailValidationException"/> when the content exceeds <paramref name="maxBytes"/>, leaving nothing
    /// behind.</summary>
    Task<(Guid Key, long Size)> SaveAsync(Stream content, long maxBytes, CancellationToken cancellationToken = default);

    Stream? OpenRead(Guid key);
}
