using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Settings;

/// <summary>The global reminder configuration (Screen 13). Timezone is read-only here; Screen 20 owns it.</summary>
public record ReminderSettingsDto(
    bool Enabled,
    bool DeadlineDayEnabled,
    int DefaultFirstDays,
    int DefaultSecondDays,
    ReminderFrequency DefaultOverdueFrequency,
    bool EmailEnabled,
    string Timezone);

/// <summary>A save of the reminder settings. There is deliberately no timezone field. Every field is
/// nullable only so that an omitted field is reported as a validation error instead of silently
/// binding to false/0; the validator requires all of them.</summary>
public record UpdateReminderSettingsRequest(
    bool? Enabled,
    bool? DeadlineDayEnabled,
    int? DefaultFirstDays,
    int? DefaultSecondDays,
    ReminderFrequency? DefaultOverdueFrequency,
    bool? EmailEnabled);
