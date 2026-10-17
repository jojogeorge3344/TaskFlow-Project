namespace TaskFlow.Application.Settings;

/// <summary>Keys, categories and defaults of the rows in the application settings table (TDD §7.2).
/// Values are stored as invariant-culture strings.</summary>
public static class SettingKeys
{
    public const string ApplicationCategory = "Application";
    public const string ReminderCategory = "Reminder";
    public const string NotificationCategory = "Notification";

    public const string ApplicationTimezone = "Application.Timezone";
    public const string ReminderEnabled = "Reminder.Enabled";
    public const string ReminderDeadlineDayEnabled = "Reminder.DeadlineDayEnabled";
    public const string ReminderDefaultFirstDays = "Reminder.DefaultFirstDays";
    public const string ReminderDefaultSecondDays = "Reminder.DefaultSecondDays";
    public const string ReminderDefaultOverdueFrequency = "Reminder.DefaultOverdueFrequency";
    public const string NotificationEmailEnabled = "Notification.EmailEnabled";

    public const string FallbackTimezone = "UTC";
    public const bool DefaultReminderEnabled = true;
    public const bool DefaultDeadlineDayEnabled = true;
    public const int DefaultFirstDays = 2;
    public const int DefaultSecondDays = 1;
    public const string DefaultOverdueFrequency = "Daily";
    public const bool DefaultEmailEnabled = false;
}
