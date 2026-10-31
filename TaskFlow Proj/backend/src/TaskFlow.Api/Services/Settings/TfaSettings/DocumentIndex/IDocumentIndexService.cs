using TaskFlow.Api.Dtos.Settings.TfaSettings;

namespace TaskFlow.Api.Services.Settings.TfaSettings.DocumentIndex;

/// <summary>The administrator's view of the TFA document index, and the Re-index now control.</summary>
public interface IDocumentIndexService
{
    DocumentIndexStatusDto GetStatus();

    /// <summary>Asks for an indexing run now. While a run is in progress nothing new is started; the returned status shows
    /// the run that is going.</summary>
    DocumentIndexStatusDto RequestReindex();
}
