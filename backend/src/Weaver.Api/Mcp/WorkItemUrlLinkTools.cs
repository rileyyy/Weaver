using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class WorkItemUrlLinkTools(IWorkItemUrlLinkService urlLinks, HttpCallerContext caller)
{
    private readonly IWorkItemUrlLinkService _urlLinks = urlLinks;
    private readonly HttpCallerContext _caller = caller;

    [McpServerTool(Name = "list_work_item_url_links", ReadOnly = true)]
    [Description("Lists a work item's URL links (links to things outside Weaver, such as a PR, a quote or a " +
        "product page), oldest first, each with its URL and optional label.")]
    public async Task<IReadOnlyList<WorkItemUrlLinkDto>> ListWorkItemUrlLinks(
        [Description("The work item's id.")] Guid workItemId,
        CancellationToken ct)
    {
        var links = await _urlLinks.ListForWorkItemAsync(workItemId, ct);
        return links.Select(WorkItemUrlLinkDto.FromView).ToList();
    }

    [McpServerTool(Name = "create_work_item_url_link", Destructive = false)]
    [Description("Adds a URL link to a work item, pointing at something outside Weaver (a PR, a commit, a " +
        "quote, a product page). Returns the new URL link.")]
    public Task<WorkItemUrlLinkDto> CreateWorkItemUrlLink(
        [Description("The work item's id.")] Guid workItemId,
        [Description("An absolute http or https address, at most 2048 characters.")] string url,
        [Description("A short name to show instead of the address, at most 200 characters. Optional.")] string? label = null,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        [Description(RunIdArgument.Description)] string? runId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseAttribution(actorLabel, runId);
            var link = await _urlLinks.CreateAsync(workItemId, url, label, ct);
            return WorkItemUrlLinkDto.FromView(link);
        });

    [McpServerTool(Name = "delete_work_item_url_link", Destructive = true)]
    [Description("Removes a URL link from a work item.")]
    public Task DeleteWorkItemUrlLink(
        [Description("The URL link's id, from list_work_item_url_links.")] Guid id,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        [Description(RunIdArgument.Description)] string? runId = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(() =>
        {
            _caller.UseAttribution(actorLabel, runId);
            return _urlLinks.DeleteAsync(id, ct);
        });
}
