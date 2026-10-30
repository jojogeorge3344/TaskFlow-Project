using TaskFlow.Api.Dtos.Settings.ReminderSettings;

namespace TaskFlow.Api.Services.Interfaces.Settings.ReminderSettings;

/// <summary>Reads and saves the global reminder settings stored in the application settings table.</summary>
public interface IReminderSettingsService
{
    /// <summary>The current settings; any value that is missing or unreadable falls back to its default.</summary>
    Task<ReminderSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves an already-validated request and returns the stored settings.</summary>
    Task<ReminderSettingsDto> UpdateAsync(int userId, UpdateReminderSettingsRequest request, CancellationToken cancellationToken = default);
}
