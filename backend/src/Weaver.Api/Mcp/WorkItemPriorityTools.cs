using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemPriorityTools(IWorkItemPriorityService priorities)
{
    private readonly IWorkItemPriorityService _priorities = priorities;

    [McpServerTool(Name = "list_work_item_priorities", ReadOnly = true)]
    [Description("Lists a Space's work item priorities (e.g. Low/Medium/High/Urgent), from least to most " +
        "urgent. Each Space has its own, and a work item can only take one of its own Space's.")]
    public async Task<IReadOnlyList<WorkItemPriorityDto>> ListWorkItemPriorities(
        [Description("The Space whose priorities to list.")] Guid spaceId,
        CancellationToken ct)
    {
        var priorities = await _priorities.GetAllAsync(spaceId, ct);
        return priorities.Select(WorkItemPriorityDto.FromEntity).ToList();
    }
}
