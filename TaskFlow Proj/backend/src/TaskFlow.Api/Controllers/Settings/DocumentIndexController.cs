using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization.Policies;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Services.Settings.TfaSettings.DocumentIndex;

namespace TaskFlow.Api.Controllers.Settings;

/// <summary>TFA Settings: the status of the document index TFA answers from, and the Re-index now control.</summary>
[ApiController]
[Route("api/v1/assistant/documents")]
[Authorize]
public class DocumentIndexController : ControllerBase
{
    private readonly IDocumentIndexService _documents;

    public DocumentIndexController(IDocumentIndexService documents)
    {
        _documents = documents;
    }

    /// <summary>The documents folder, embedding model, file and chunk counts, last run and last error.</summary>
    [HttpGet("status")]
    [RequirePermission(PermissionKeys.SettingsAiView)]
    public ActionResult<DocumentIndexStatusDto> GetStatus() => Ok(_documents.GetStatus());

    /// <summary>Checks the documents folder now. A request while a run is in progress starts nothing new.</summary>
    [HttpPost("reindex")]
    [RequirePermission(PermissionKeys.SettingsAiEdit)]
    public ActionResult<DocumentIndexStatusDto> Reindex() => Accepted(_documents.RequestReindex());
}
