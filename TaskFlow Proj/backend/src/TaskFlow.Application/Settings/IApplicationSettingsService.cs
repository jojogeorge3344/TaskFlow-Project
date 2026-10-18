namespace TaskFlow.Application.Settings;

/// <summary>Reads and saves the application-level settings (Screen 20) stored in the application
/// settings table.</summary>
public interface IApplicationSettingsService
{
    /// <summary>The current settings; any value that is missing or unreadable falls back to its default.</summary>
    Task<ApplicationSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves an already-validated request and returns the stored settings.</summary>
    Task<ApplicationSettingsDto> UpdateAsync(int userId, UpdateApplicationSettingsRequest request, CancellationToken cancellationToken = default);
}
