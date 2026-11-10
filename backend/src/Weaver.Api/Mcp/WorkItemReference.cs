using ModelContextProtocol;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Mcp;

/// <summary>
/// A read tool's way to name one work item: by id, or by the Number (#N) people use. A tool
/// takes both as optional arguments and exactly one must be given.
/// </summary>
public static class WorkItemReference
{
    public const string IdDescription = "The work item's id.";
    public const string NumberDescription = "The work item's Number, without the # (123 for #123).";

    public static async Task<WorkItem> FindByIdOrNumberAsync(
        this IWorkItemService workItems,
        Guid? id,
        int? number,
        CancellationToken ct) =>
        (id, number) switch
        {
            ({ } byId, null) => await workItems.GetByIdAsync(byId, ct)
                ?? throw new McpException($"Work item {byId} was not found."),
            (null, { } byNumber) => await workItems.GetByNumberAsync(byNumber, ct)
                ?? throw new McpException($"Work item #{byNumber} was not found."),
            _ => throw new McpException("Give exactly one of id and number."),
        };
}
