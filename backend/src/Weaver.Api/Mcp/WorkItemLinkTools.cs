using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemLinkTools(IWorkItemLinkService links, HttpCallerContext caller)
{
    private const string TypeDescription =
        "How the work item relates to the other one: Related (associated, no order), Blocks (the work item " +
        "must be done before the other can be), BlockedBy (the other must be done first), Duplicates (the work " +
        "item is a duplicate of the other) or DuplicatedBy (the other is a duplicate of it).";

    private readonly IWorkItemLinkService _links = links;
    private readonly HttpCallerContext _caller = caller;

    [McpServerTool(Name = "list_work_item_links", ReadOnly = true)]
    [Description("Lists every work item linked to a given work item, oldest first, regardless of which side " +
        "created the link. Each link's type is read from the given work item's side: Related, Blocks, " +
        "BlockedBy, Duplicates or DuplicatedBy. Linked items can be in other Spaces.")]
    public async Task<IReadOnlyList<WorkItemLinkDto>> ListWorkItemLinks(
        [Description("The work item's id.")] Guid workItemId,
        CancellationToken ct)
    {
        var links = await _links.ListForWorkItemAsync(workItemId, ct);
        return links.Select(WorkItemLinkDto.FromView).ToList();
    }

    [McpServerTool(Name = "create_work_item_link", Destructive = false)]
    [Description("Links two work items, which may be in different Spaces. A blocks link is stored once: " +
        "creating \"A BlockedBy B\" is the same link as \"B Blocks A\", and each side lists it from its own " +
        "view; duplicates works the same way. Two work items can have only one link, of any type. Rejects " +
        "linking a work item to itself, a link that already exists (in either direction), and a blocks link " +
        "against one that already exists the other way. Returns the link seen from workItemId.")]
    public Task<WorkItemLinkDto> CreateWorkItemLink(
        [Description("Id of the work item to link from.")] Guid workItemId,
        [Description("Id of the work item to link it to.")] Guid targetWorkItemId,
        [Description(TypeDescription)] WorkItemLinkRelation type,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        [Description(RunIdArgument.Description)] string? runId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseAttribution(actorLabel, runId);
            var link = await _links.CreateAsync(workItemId, targetWorkItemId, type, ct);
            return WorkItemLinkDto.FromView(link);
        });

    [McpServerTool(Name = "delete_work_item_link", Destructive = true)]
    [Description("Removes a link between two work items, whatever its type, from both sides.")]
    public Task DeleteWorkItemLink(
        [Description("The link's id, from list_work_item_links.")] Guid id,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        [Description(RunIdArgument.Description)] string? runId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(() =>
        {
            _caller.UseAttribution(actorLabel, runId);
            return _links.DeleteAsync(id, ct);
        });
}
