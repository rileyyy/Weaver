using System.ComponentModel;
using ModelContextProtocol.Server;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class CommentTools(ICommentService comments, HttpCallerContext caller)
{
    private readonly ICommentService _comments = comments;
    private readonly HttpCallerContext _caller = caller;

    [McpServerTool(Name = "list_comments", ReadOnly = true)]
    [Description("Lists every comment on a work item.")]
    public async Task<IReadOnlyList<CommentDto>> ListComments(
        [Description("The work item's id.")] Guid workItemId,
        CancellationToken ct)
    {
        var comments = await _comments.ListForWorkItemAsync(workItemId, ct);
        return comments.Select(CommentDto.FromView).ToList();
    }

    [McpServerTool(Name = "add_comment", Destructive = false)]
    [Description("Adds a comment to a work item, authored by the calling user.")]
    public Task<CommentDto> AddComment(
        [Description("The work item's id.")] Guid workItemId,
        [Description("The comment's text.")] string body,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseActorLabel(actorLabel);
            var comment = await _comments.CreateAsync(workItemId, body, ct);
            return CommentDto.FromView(comment);
        });

    [McpServerTool(Name = "update_comment", Destructive = false, Idempotent = true)]
    [Description("Updates a comment's text. Only the comment's own author may do this.")]
    public Task<CommentDto> UpdateComment(
        [Description("The comment's id.")] Guid id,
        [Description("The comment's new text.")] string body,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            _caller.UseActorLabel(actorLabel);
            var comment = await _comments.UpdateAsync(id, body, ct);
            return CommentDto.FromView(comment);
        });

    [McpServerTool(Name = "delete_comment", Destructive = true)]
    [Description("Deletes a comment. Only the comment's own author may do this.")]
    public Task DeleteComment(
        [Description("The comment's id.")] Guid id,
        [Description(ActorLabelArgument.Description)] string? actorLabel = null,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(() =>
        {
            _caller.UseActorLabel(actorLabel);
            return _comments.DeleteAsync(id, ct);
        });
}
