namespace TaskFlow.Api.Integrations.Ai.Embeddings;

/// <summary>What the text is for. <c>nomic-embed-text</c> is trained with a different prefix for each.</summary>
public enum EmbeddingPurpose
{
    Document,
    Query
}

/// <summary>The Ollama server and model an embedding call uses.</summary>
public record EmbeddingEndpoint(string BaseUrl, string Model);

/// <summary>Why embeddings could not be made.</summary>
public enum EmbeddingFailureKind
{
    Unreachable,
    Timeout,
    ModelNotFound,
    Unexpected
}

/// <summary>Embeddings cannot be made right now. The message is fixed text for the administrator's status card and the log:
/// it never holds document text.</summary>
public class EmbeddingUnavailableException : Exception
{
    public EmbeddingUnavailableException(EmbeddingFailureKind kind, EmbeddingEndpoint endpoint, Exception? inner = null)
        : base(Describe(kind, endpoint), inner)
    {
        Kind = kind;
    }

    public EmbeddingFailureKind Kind { get; }

    private static string Describe(EmbeddingFailureKind kind, EmbeddingEndpoint endpoint) => kind switch
    {
        EmbeddingFailureKind.Unreachable => $"The Ollama server at {endpoint.BaseUrl} cannot be reached. Start Ollama and try again.",
        EmbeddingFailureKind.Timeout => $"The Ollama server at {endpoint.BaseUrl} did not answer in time.",
        EmbeddingFailureKind.ModelNotFound => $"The embedding model '{endpoint.Model}' is not installed in Ollama. Run: ollama pull {endpoint.Model}",
        _ => $"The Ollama server at {endpoint.BaseUrl} returned an unexpected embedding response."
    };
}
