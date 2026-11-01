namespace TaskFlow.Api.Repositories.Assistant.Documents;

/// <summary>A stored chunk with the document it belongs to, as read for the search index.</summary>
public record DocumentChunkRow(
    int ChunkId,
    string RelativePath,
    string Title,
    string EmbeddingModel,
    string HeadingPath,
    string Text,
    byte[] Embedding,
    int Dimensions);
