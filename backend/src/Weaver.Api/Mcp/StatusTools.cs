using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class StatusTools
{
    private readonly IStatusService _statuses;

    public StatusTools(IStatusService statuses)
    {
        _statuses = statuses;
    }

    [McpServerTool(Name = "list_statuses", ReadOnly = true)]
    [Description("Lists every status column, in board display order.")]
    public async Task<IReadOnlyList<StatusDto>> ListStatuses(CancellationToken ct)
    {
        var statuses = await _statuses.GetAllAsync(ct);
        return statuses.Select(StatusDto.FromEntity).ToList();
    }
}
