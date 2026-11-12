using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

/// <summary>
/// Read-only on purpose: Spaces are admin configuration, which agents never change (ADR-0004),
/// so there is no tool to create, rename or configure one.
/// </summary>
[McpServerToolType]
public class SpaceTools(ISpaceService spaces, ISpaceHistoryService history)
{
    private readonly ISpaceService _spaces = spaces;
    private readonly ISpaceHistoryService _history = history;

    [McpServerTool(Name = "list_spaces", ReadOnly = true)]
    [Description("Lists every Space (e.g. Home, Village, Software), by name. Every work item belongs to " +
        "exactly one Space; top-level items are created in one and children join their parent's. " +
        "doneHidingDays is the Space's done-hiding period: the Board leaves out Done items completed " +
        "more than that many days ago (search_work_items does the same with applyDoneHiding).")]
    public async Task<IReadOnlyList<SpaceDto>> ListSpaces(CancellationToken ct)
    {
        var spaces = await _spaces.GetAllAsync(ct);
        return spaces.Select(SpaceDto.FromEntity).ToList();
    }

    [McpServerTool(Name = "list_space_history", ReadOnly = true)]
    [Description("Lists every change to a Space, newest first: its creation (field created, new value the " +
        "name), renames (field name) and done-hiding period changes (field doneHidingDays), each with who " +
        "made it and when. Changes to the Space's work items aren't here: read those with " +
        "get_work_item_context or list_changes_since.")]
    public Task<IReadOnlyList<SpaceHistoryEntryDto>> ListSpaceHistory(
        [Description("The Space's id, from list_spaces.")] Guid spaceId,
        CancellationToken ct) =>
        McpExceptionTranslation.TranslateAsync<IReadOnlyList<SpaceHistoryEntryDto>>(async () =>
            (await _history.ListForSpaceAsync(spaceId, ct)).Select(SpaceHistoryEntryDto.FromView).ToList());
}
