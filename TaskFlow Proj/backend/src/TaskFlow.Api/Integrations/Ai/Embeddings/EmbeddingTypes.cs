namespace TaskFlow.Api.Integrations.Ai.Embeddings;

/// <summary>What the text is for. <c>gemini-embedding-2</c> is told in the text itself, with a different prefix for each.</summary>
public enum EmbeddingPurpose
{
    Document,
    Query
}

/// <summary>The Google Gemini model, vector size and API key an embedding call uses. The key is decrypted for the call
/// only and is never logged, so <see cref="ToString"/> leaves it out.</summary>
public record EmbeddingEndpoint(string Model, int Dimensions, string ApiKey)
{
    public override string ToString() => $"{nameof(EmbeddingEndpoint)} {{ Model = {Model}, Dimensions = {Dimensions} }}";
}

/// <summary>Why embeddings could not be made.</summary>
public enum EmbeddingFailureKind
{
    MissingKey,
    InvalidKey,
    RateLimited,
    Unreachable,
    Timeout,
    ModelNotFound,
    Unexpected
}

/// <summary>Embeddings cannot be made right now. The message is fixed text for the server log: it never holds the API key
/// or document text.</summary>
public class EmbeddingUnavailableException : Exception
{
    public EmbeddingUnavailableException(EmbeddingFailureKind kind, string model, Exception? inner = null)
        : base(Describe(kind, model), inner)
    {
        Kind = kind;
    }

    public EmbeddingFailureKind Kind { get; }

    private static string Describe(EmbeddingFailureKind kind, string model) => kind switch
    {
        EmbeddingFailureKind.MissingKey => "No Google Gemini API key is saved. Save a Google Gemini API key in TFA Settings to enable document search.",
        EmbeddingFailureKind.InvalidKey => "Google Gemini did not accept the saved API key for document search. Save a valid key in TFA Settings.",
        EmbeddingFailureKind.RateLimited => "Google Gemini's rate limit was reached while embedding documents. The next check continues where this one stopped.",
        EmbeddingFailureKind.Unreachable => "Google Gemini cannot be reached for document search. Check the internet connection.",
        EmbeddingFailureKind.Timeout => "Google Gemini did not answer the embedding request in time.",
        EmbeddingFailureKind.ModelNotFound => $"The embedding model '{model}' is not available on Google Gemini. Check DocumentKnowledge:EmbeddingModel.",
        _ => "Google Gemini returned an unexpected embedding response."
    };
}
