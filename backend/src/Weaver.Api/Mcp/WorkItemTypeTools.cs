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
    [Description("Lists a Space's Types (e.g. Project/Goal/Task), in display order. Each Space " +
        "has its own, and a work item can only take one of its own Space's. Any Type can be nested under any other.")]
    public async Task<IReadOnlyList<WorkItemTypeDto>> ListWorkItemTypes(
        [Description("The Space whose Types to list.")] Guid spaceId,
        CancellationToken ct)
    {
        var types = await _types.GetAllAsync(spaceId, ct);
        return types.Select(WorkItemTypeDto.FromEntity).ToList();
    }
}
