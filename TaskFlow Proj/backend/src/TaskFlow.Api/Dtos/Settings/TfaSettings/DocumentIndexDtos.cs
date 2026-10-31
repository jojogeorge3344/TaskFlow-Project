namespace TaskFlow.Api.Dtos.Settings.TfaSettings;

/// <summary>The TFA document index as the administrator's Document search card shows it.</summary>
public record DocumentIndexStatusDto(
    bool Enabled,
    string SourcePath,
    bool FolderFound,
    string EmbeddingModel,
    int FileCount,
    int ChunkCount,
    DateTime? LastIndexedAt,
    bool IsRunning,
    string? LastError,
    string? LastErrorFile);
