using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Projects;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.Api.Controllers;

/// <summary>The positions people can hold on a project (QA, Developer, ...). A label only; it grants no access.</summary>
[ApiController]
[Route("api/v1/positions")]
public class PositionsController : ControllerBase
{
    private readonly IPositionService _positionService;

    public PositionsController(IPositionService positionService)
    {
        _positionService = positionService;
    }

    [HttpGet]
    [RequirePermission(PermissionKeys.ProjectsView)]
    public async Task<ActionResult<IReadOnlyCollection<PositionDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _positionService.GetAllAsync(cancellationToken));

    /// <summary>Creates the position, or returns the existing one when the name matches ignoring case.</summary>
    [HttpPost]
    [RequirePermission(PermissionKeys.PositionsCreate)]
    public async Task<ActionResult<PositionDto>> GetOrCreate([FromBody] CreatePositionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _positionService.GetOrCreateAsync(request.Name, cancellationToken));
    }
}
