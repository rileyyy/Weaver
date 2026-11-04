using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

/// <summary>
/// Types and their custom fields are read-only here: only human admins define them (ADR-0002),
/// so there is deliberately no tool that changes a definition.
/// </summary>
[McpServerToolType]
public class WorkItemTypeTools(IWorkItemTypeService types, ICustomFieldService fields)
{
    private readonly IWorkItemTypeService _types = types;
    private readonly ICustomFieldService _fields = fields;

    [McpServerTool(Name = "list_work_item_types", ReadOnly = true)]
    [Description("Lists a Space's Types (e.g. Project/Goal/Task), in display order, each with its custom " +
        "fields (id, name, kind and, for select fields, options). Each Space has its own Types, and a work item " +
        "can only take one of its own Space's. Any Type can be nested under any other. Set an item's field " +
        "values with set_work_item_fields; field definitions can only be changed by an admin in the app.")]
    public async Task<IReadOnlyList<WorkItemTypeWithFieldsDto>> ListWorkItemTypes(
        [Description("The Space whose Types to list.")] Guid spaceId,
        CancellationToken ct)
    {
        var types = await _types.GetAllAsync(spaceId, ct);
        var fieldsByType = (await _fields.GetAllAsync(types.Select(t => t.Id).ToList(), ct)).ToLookup(f => f.TypeId);
        return types.Select(t => WorkItemTypeWithFieldsDto.FromEntity(t, fieldsByType[t.Id])).ToList();
    }
}
