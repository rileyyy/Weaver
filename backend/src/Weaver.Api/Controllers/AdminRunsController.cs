using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>Run reports: what each agent run changed, for administrators (ADR-0010).</summary>
[ApiController]
[Route("api/admin/runs")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class AdminRunsController(IRunReportService runs) : ControllerBase
{
    private readonly IRunReportService _runs = runs;

    /// <summary>Runs newest first, by their last change.</summary>
    [HttpGet]
    public async Task<ActionResult<RunReportPageDto>> List(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = RunReportService.DefaultPageSize)
    {
        var page = await _runs.ListAsync(offset, limit);
        return Ok(RunReportPageDto.FromPage(page));
    }

    /// <summary>
    /// The items one run changed, in the order it first changed each, with that run's entries.
    /// The run id is a query parameter, not part of the path, because it's free text. Leave out
    /// <paramref name="apiKeyId"/> for a run sent without a key.
    /// </summary>
    [HttpGet("changes")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ChangedWorkItemDto>>> ListChanges(
        [FromQuery, BindRequired] Guid accountId,
        [FromQuery, BindRequired] string runId,
        [FromQuery] Guid? apiKeyId = null)
    {
        var changes = await _runs.ListChangesAsync(new RunKey(accountId, apiKeyId, runId));
        return Ok(changes.Select(ChangedWorkItemDto.FromChange));
    }
}
