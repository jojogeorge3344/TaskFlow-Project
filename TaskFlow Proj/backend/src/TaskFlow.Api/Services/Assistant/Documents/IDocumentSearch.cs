namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>Finds the document text TFA is given for a message (design D1 and D9).</summary>
public interface IDocumentSearch
{
    /// <summary>Never throws for an unavailable embedding server or an empty index: those come back as
    /// <see cref="DocumentContextKind.Unavailable"/>.</summary>
    Task<DocumentContext> SearchAsync(string message, string? previousUserMessage, CancellationToken cancellationToken = default);
}
