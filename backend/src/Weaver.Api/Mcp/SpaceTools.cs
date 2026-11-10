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
public class SpaceTools(ISpaceService spaces)
{
    private readonly ISpaceService _spaces = spaces;

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
}
