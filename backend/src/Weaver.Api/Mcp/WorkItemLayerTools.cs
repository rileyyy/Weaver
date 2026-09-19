using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemLayerTools
{
    private readonly IWorkItemLayerService _layers;

    public WorkItemLayerTools(IWorkItemLayerService layers)
    {
        _layers = layers;
    }

    [McpServerTool(Name = "list_work_item_layers", ReadOnly = true)]
    [Description("Lists every work item layer (e.g. Project/Goal/Task), in display order.")]
    public async Task<IReadOnlyList<WorkItemLayerDto>> ListWorkItemLayers(CancellationToken ct)
    {
        var layers = await _layers.GetAllAsync(ct);
        return layers.Select(WorkItemLayerDto.FromEntity).ToList();
    }
}
