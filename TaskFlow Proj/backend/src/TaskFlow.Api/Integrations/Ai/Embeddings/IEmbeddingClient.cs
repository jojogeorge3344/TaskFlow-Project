namespace TaskFlow.Api.Integrations.Ai.Embeddings;

/// <summary>Turns text into embedding vectors on a local Ollama server.</summary>
public interface IEmbeddingClient
{
    /// <summary>One L2-normalised vector per input, in input order.</summary>
    /// <exception cref="EmbeddingUnavailableException">The server or the model cannot be used.</exception>
    Task<float[][]> EmbedAsync(EmbeddingEndpoint endpoint, IReadOnlyList<string> inputs, EmbeddingPurpose purpose, CancellationToken cancellationToken = default);
}
