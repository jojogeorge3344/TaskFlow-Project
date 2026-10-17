using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Application.Reports;

namespace TaskFlow.Api.Controllers;

/// <summary>Task reports and CSV export (Screen 17). Administrator and Manager only.</summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = AuthPolicies.ManagerOrAdmin)]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IValidator<ReportFilter> _filterValidator;

    public ReportController(IReportService reportService, IValidator<ReportFilter> filterValidator)
    {
        _reportService = reportService;
        _filterValidator = filterValidator;
    }

    /// <summary>Totals and completion summary for the filtered scope.</summary>
    [HttpGet]
    public async Task<ActionResult<ReportSummaryDto>> Get(
        [FromQuery] ReportFilter filter,
        CancellationToken cancellationToken)
    {
        var validation = await _filterValidator.ValidateAsync(filter, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        return Ok(await _reportService.GetSummaryAsync(filter, cancellationToken));
    }

    /// <summary>The filtered task rows as a CSV attachment.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] ReportFilter filter,
        CancellationToken cancellationToken)
    {
        var validation = await _filterValidator.ValidateAsync(filter, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var rows = await _reportService.GetRowsAsync(filter, cancellationToken);
        var csv = ReportCsv.Write(rows);

        return File(Encoding.UTF8.GetBytes(csv), ReportCsv.ContentType, ReportCsv.FileName);
    }
}
