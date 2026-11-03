using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemTypeTools(IWorkItemTypeService types)
{
    private readonly IWorkItemTypeService _types = types;

    [McpServerTool(Name = "list_work_item_types", ReadOnly = true)]
    [Description("Lists every work item type (e.g. Project/Goal/Task), in display order.")]
    public async Task<IReadOnlyList<WorkItemTypeDto>> ListWorkItemTypes(CancellationToken ct)
    {
        var types = await _types.GetAllAsync(ct);
        return types.Select(WorkItemTypeDto.FromEntity).ToList();
    }
}
