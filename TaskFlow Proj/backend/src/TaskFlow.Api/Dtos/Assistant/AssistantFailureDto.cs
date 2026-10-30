using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Dtos.Assistant;

/// <summary>A classified provider problem as shown to the user: a fixed message plus the other providers they could
/// choose. The raw provider error is never part of it.</summary>
public record AssistantFailureDto(
    ProviderFailureKind Kind,
    AiProviderKind Provider,
    string Message,
    int? RetryAfterSeconds,
    IReadOnlyList<AiProviderOptionDto> Alternatives);
