using Microsoft.Extensions.Options;
using TaskFlow.Application.Mail;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Mail;

/// <summary>Stores attachment files on the local disk as <c>&lt;root&gt;/&lt;first two chars&gt;/&lt;guid&gt;</c>. The user's file
/// name is never part of the path, so names cannot escape the folder.</summary>
public class LocalMailFileStore : IMailFileStore
{
    private readonly string _root;

    public LocalMailFileStore(IOptions<MailOptions> options)
        : this(ResolveRoot(options.Value.AttachmentsPath))
    {
    }

    public LocalMailFileStore(string root)
    {
        _root = root;
    }

    public static string ResolveRoot(string configured) =>
        Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(DatabasePathResolver.FindSolutionDirectory(), configured));

    public async Task<(Guid Key, long Size)> SaveAsync(Stream content, long maxBytes, CancellationToken cancellationToken = default)
    {
        var key = Guid.NewGuid();
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        long size = 0;
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                size += read;
                if (size > maxBytes)
                {
                    throw new MailValidationException("File", $"A file can be at most {maxBytes / (1024 * 1024)} MB.");
                }

                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        catch
        {
            File.Delete(path);
            throw;
        }

        return (key, size);
    }

    public Stream? OpenRead(Guid key)
    {
        var path = PathFor(key);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true) : null;
    }

    private string PathFor(Guid key)
    {
        var name = key.ToString("N");
        return Path.Combine(_root, name[..2], name);
    }
}
