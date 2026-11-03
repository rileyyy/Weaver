using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

/// <summary>
/// Read-only on purpose: Spaces are admin configuration, which agents never change (ADR-0004),
/// so there is no tool to create or rename one.
/// </summary>
[McpServerToolType]
public class SpaceTools
{
    private readonly ISpaceService _spaces;

    public SpaceTools(ISpaceService spaces)
    {
        _spaces = spaces;
    }

    [McpServerTool(Name = "list_spaces", ReadOnly = true)]
    [Description("Lists every Space (e.g. Home, Village, Software), by name. Every work item belongs to " +
        "exactly one Space; top-level items are created in one and children join their parent's.")]
    public async Task<IReadOnlyList<SpaceDto>> ListSpaces(CancellationToken ct)
    {
        var spaces = await _spaces.GetAllAsync(ct);
        return spaces.Select(SpaceDto.FromEntity).ToList();
    }
}
