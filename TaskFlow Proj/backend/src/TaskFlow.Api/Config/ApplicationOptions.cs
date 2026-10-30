using TaskFlow.Api.Common.Constants;

namespace TaskFlow.Api.Config;

/// <summary>The <c>Application</c> configuration section. Timezone seeds the
/// <c>Application.Timezone</c> setting and is the fallback while that row is missing.</summary>
public class ApplicationOptions
{
    public const string SectionName = "Application";

    public string Timezone { get; set; } = SettingKeys.FallbackTimezone;
}
