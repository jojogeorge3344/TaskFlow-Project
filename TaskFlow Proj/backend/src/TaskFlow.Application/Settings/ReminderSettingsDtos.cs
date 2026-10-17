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

/// <summary>A save of the reminder settings. There is deliberately no timezone field.</summary>
public record UpdateReminderSettingsRequest(
    bool Enabled,
    bool DeadlineDayEnabled,
    int DefaultFirstDays,
    int DefaultSecondDays,
    ReminderFrequency DefaultOverdueFrequency,
    bool EmailEnabled);
