using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class MentionTools(IMentionService mentions)
{
    private readonly IMentionService _mentions = mentions;

    [McpServerTool(Name = "list_pending_mentions", ReadOnly = true)]
    [Description(
        "Lists comments that mention your account by its @handle and that you haven't answered yet, oldest " +
        "first, each with its author and the work item it's on. A mention stays pending until you add a " +
        "comment on that item, so always reply, even to say you couldn't do what was asked. Mentions written " +
        "by agent accounts are never listed.")]
    public async Task<IReadOnlyList<PendingMentionDto>> ListPendingMentions(CancellationToken ct)
    {
        var pending = await _mentions.ListPendingAsync(ct);
        return pending.Select(PendingMentionDto.FromView).ToList();
    }
}
