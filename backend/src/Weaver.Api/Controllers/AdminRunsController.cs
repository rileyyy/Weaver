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
public class AdminRunsController(IRunReportService runs, IRunUndoService undo) : ControllerBase
{
    private readonly IRunReportService _runs = runs;
    private readonly IRunUndoService _undo = undo;

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
    /// The run id is a query parameter, not part of the path, because it's free text.
    /// </summary>
    [HttpGet("changes")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ChangedWorkItemDto>>> ListChanges(
        [FromQuery, BindRequired] Guid accountId,
        [FromQuery, BindRequired] Guid apiKeyId,
        [FromQuery, BindRequired] string runId)
    {
        var changes = await _runs.ListChangesAsync(new RunKey(accountId, apiKeyId, runId));
        return Ok(changes.Select(ChangedWorkItemDto.FromChange));
    }

    /// <summary>
    /// Undoes what one run did (ticket 54): its additions are removed and the fields it changed
    /// get their old values back, except where someone else has changed them since. Comments are
    /// kept. The undo is recorded in history as the caller's, and repeating it changes nothing.
    /// </summary>
    [HttpPost("undo")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RunUndoResultDto>> Undo(
        [FromQuery, BindRequired] Guid accountId,
        [FromQuery, BindRequired] Guid apiKeyId,
        [FromQuery, BindRequired] string runId)
    {
        var result = await _undo.UndoAsync(new RunKey(accountId, apiKeyId, runId));
        return Ok(RunUndoResultDto.FromResult(result));
    }
}
