using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemLinkTools
{
    private readonly IWorkItemLinkService _links;

    public WorkItemLinkTools(IWorkItemLinkService links)
    {
        _links = links;
    }

    [McpServerTool(Name = "list_work_item_links", ReadOnly = true)]
    [Description("Lists every work item linked to a given work item, regardless of which side created the link.")]
    public async Task<IReadOnlyList<WorkItemLinkDto>> ListWorkItemLinks(
        [Description("The work item's id.")] Guid workItemId,
        CancellationToken ct)
    {
        var links = await _links.ListForWorkItemAsync(workItemId, ct);
        return links.Select(l => WorkItemLinkDto.FromEntity(l, workItemId)).ToList();
    }

    [McpServerTool(Name = "create_work_item_link", Destructive = false)]
    [Description("Creates a symmetric \"related to\" link between two work items. Rejects linking a " +
        "work item to itself or a link that already exists (in either direction).")]
    public Task<WorkItemLinkDto> CreateWorkItemLink(
        [Description("Id of the first work item.")] Guid workItemId,
        [Description("Id of the work item to link it to.")] Guid targetWorkItemId,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var link = await _links.CreateAsync(workItemId, targetWorkItemId, ct);
            return WorkItemLinkDto.FromEntity(link, workItemId);
        });

    [McpServerTool(Name = "delete_work_item_link", Destructive = true)]
    [Description("Removes a link between two work items.")]
    public Task DeleteWorkItemLink(
        [Description("The link's id.")] Guid id,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(() => _links.DeleteAsync(id, ct));
}
