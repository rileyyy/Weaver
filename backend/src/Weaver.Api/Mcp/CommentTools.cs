using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Mcp;

[McpServerToolType]
public class CommentTools
{
    private readonly ICommentService _comments;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CommentTools(ICommentService comments, IHttpContextAccessor httpContextAccessor)
    {
        _comments = comments;
        _httpContextAccessor = httpContextAccessor;
    }

    private Guid CurrentUserId =>
        (_httpContextAccessor.HttpContext ?? throw new McpException("No HTTP request is available to authenticate this call."))
        .User.GetUserId();

    [McpServerTool(Name = "list_comments", ReadOnly = true)]
    [Description("Lists every comment on a work item.")]
    public async Task<IReadOnlyList<CommentDto>> ListComments(
        [Description("The work item's id.")] Guid workItemId,
        CancellationToken ct)
    {
        var comments = await _comments.ListForWorkItemAsync(workItemId, ct);
        return comments.Select(CommentDto.FromEntity).ToList();
    }

    [McpServerTool(Name = "add_comment", Destructive = false)]
    [Description("Adds a comment to a work item, authored by the calling user.")]
    public Task<CommentDto> AddComment(
        [Description("The work item's id.")] Guid workItemId,
        [Description("The comment's text.")] string body,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var comment = await _comments.CreateAsync(workItemId, CurrentUserId, body, ct);
            return CommentDto.FromEntity(comment);
        });

    [McpServerTool(Name = "update_comment", Destructive = false, Idempotent = true)]
    [Description("Updates a comment's text. Only the comment's own author may do this.")]
    public Task<CommentDto> UpdateComment(
        [Description("The comment's id.")] Guid id,
        [Description("The comment's new text.")] string body,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var comment = await _comments.UpdateAsync(id, CurrentUserId, body, ct);
            return CommentDto.FromEntity(comment);
        });

    [McpServerTool(Name = "delete_comment", Destructive = true)]
    [Description("Deletes a comment. Only the comment's own author may do this.")]
    public Task DeleteComment(
        [Description("The comment's id.")] Guid id,
        CancellationToken ct = default) =>
        McpExceptionTranslation.TranslateAsync(() => _comments.DeleteAsync(id, CurrentUserId, ct));
}
