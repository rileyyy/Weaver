using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemPriorityTools
{
    private readonly IWorkItemPriorityService _priorities;

    public WorkItemPriorityTools(IWorkItemPriorityService priorities)
    {
        _priorities = priorities;
    }

    [McpServerTool(Name = "list_work_item_priorities", ReadOnly = true)]
    [Description("Lists every work item priority (e.g. Low/Medium/High/Urgent), from least to most urgent.")]
    public async Task<IReadOnlyList<WorkItemPriorityDto>> ListWorkItemPriorities(CancellationToken ct)
    {
        var priorities = await _priorities.GetAllAsync(ct);
        return priorities.Select(WorkItemPriorityDto.FromEntity).ToList();
    }
}
