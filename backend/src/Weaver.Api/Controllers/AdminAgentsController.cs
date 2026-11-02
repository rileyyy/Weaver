using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

/// <summary>Agent accounts and their API keys, for administrators (ADR-0004).</summary>
[ApiController]
[Route("api/admin/agents")]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public class AdminAgentsController : ControllerBase
{
    private readonly IAgentAccountService _agents;

    public AdminAgentsController(IAgentAccountService agents)
    {
        _agents = agents;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentAccountDto>>> GetAll()
    {
        var agents = await _agents.ListAsync();
        return Ok(agents.Select(AgentAccountDto.FromEntity));
    }

    /// <summary>An agent account has no view of its own, so the Location header points at the list.</summary>
    [HttpPost]
    [ProducesResponseType<AgentAccountDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AgentAccountDto>> Create(CreateAgentAccountRequest request)
    {
        var agent = await _agents.CreateAsync(request.Name, request.Description);
        return CreatedAtAction(nameof(GetAll), AgentAccountDto.FromEntity(agent));
    }

    [HttpPut("{id:guid}/active")]
    public async Task<ActionResult<AgentAccountDto>> SetActive(Guid id, SetUserActiveRequest request) =>
        Ok(AgentAccountDto.FromEntity(await _agents.SetActiveAsync(id, request.IsActive)));

    [HttpGet("{id:guid}/keys")]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> GetKeys(Guid id)
    {
        var keys = await _agents.ListKeysAsync(id);
        return Ok(keys.Select(ApiKeyDto.FromEntity));
    }

    /// <summary>The response is the only time the key is shown; the Location header points at the key list.</summary>
    [HttpPost("{id:guid}/keys")]
    [ProducesResponseType<IssuedApiKeyDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<IssuedApiKeyDto>> CreateKey(Guid id, CreateApiKeyRequest request)
    {
        var issued = await _agents.IssueKeyAsync(id, request.Label, request.Scope);
        return CreatedAtAction(nameof(GetKeys), new { id }, IssuedApiKeyDto.FromIssued(issued));
    }

    /// <summary>Revokes rather than deletes: the key stays in the list, marked revoked.</summary>
    [HttpDelete("{id:guid}/keys/{keyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeKey(Guid id, Guid keyId)
    {
        await _agents.RevokeKeyAsync(id, keyId);
        return NoContent();
    }
}
