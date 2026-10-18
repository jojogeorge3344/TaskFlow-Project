using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Settings;

/// <summary>The application-level configuration (Screen 20). These values are non-secret and are
/// readable by any signed-in user so screens can reflect them (brand name, default priority).</summary>
public record ApplicationSettingsDto(
    string Name,
    string Timezone,
    TaskPriority DefaultPriority,
    bool InAppNotificationsEnabled);

/// <summary>A save of the application settings. Every field is nullable only so that an omitted
/// field is reported as a validation error instead of silently binding to a default; the validator
/// requires all of them.</summary>
public record UpdateApplicationSettingsRequest(
    string? Name,
    string? Timezone,
    TaskPriority? DefaultPriority,
    bool? InAppNotificationsEnabled);
