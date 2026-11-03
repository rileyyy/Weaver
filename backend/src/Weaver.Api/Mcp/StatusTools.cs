using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class StatusTools(IStatusService statuses)
{
    private readonly IStatusService _statuses = statuses;

    [McpServerTool(Name = "list_statuses", ReadOnly = true)]
    [Description("Lists a Space's status columns, in board display order. Each Space has its own " +
        "statuses, and a work item can only take one of its own Space's (find Spaces with list_spaces).")]
    public async Task<IReadOnlyList<StatusDto>> ListStatuses(
        [Description("The Space whose statuses to list.")] Guid spaceId,
        CancellationToken ct)
    {
        var statuses = await _statuses.GetAllAsync(spaceId, ct);
        return statuses.Select(StatusDto.FromEntity).ToList();
    }
}
