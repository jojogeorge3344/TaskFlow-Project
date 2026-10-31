namespace TaskFlow.Api.Services.Assistant.Documents.Rules;

/// <summary>Stores an embedding as little-endian float32 bytes, the format of <c>DocumentChunk.Embedding</c>.</summary>
public static class EmbeddingVectors
{
    public static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        for (var index = 0; index < vector.Length; index++)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * sizeof(float)), vector[index]);
        }

        return bytes;
    }

    /// <summary>The vector, or null when the bytes do not hold <paramref name="dimensions"/> values.</summary>
    public static float[]? FromBytes(byte[] bytes, int dimensions)
    {
        if (dimensions <= 0 || bytes.Length != dimensions * sizeof(float))
        {
            return null;
        }

        var vector = new float[dimensions];
        for (var index = 0; index < dimensions; index++)
        {
            vector[index] = System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(index * sizeof(float)));
        }

        return vector;
    }
}
