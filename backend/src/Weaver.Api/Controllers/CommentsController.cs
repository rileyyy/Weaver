using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Infrastructure.Services;

namespace Weaver.Api.Controllers;

[ApiController]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _comments;

    public CommentsController(ICommentService comments)
    {
        _comments = comments;
    }

    [HttpGet("api/work-items/{workItemId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> ListForWorkItem(Guid workItemId)
    {
        var comments = await _comments.ListForWorkItemAsync(workItemId);
        return Ok(comments.Select(CommentDto.FromEntity));
    }

    [HttpPost("api/work-items/{workItemId:guid}/comments")]
    public async Task<ActionResult<CommentDto>> Create(Guid workItemId, CreateCommentRequest request)
    {
        var comment = await _comments.CreateAsync(workItemId, User.GetUserId(), request.Body);
        return Ok(CommentDto.FromEntity(comment));
    }

    [HttpPut("api/comments/{id:guid}")]
    public async Task<ActionResult<CommentDto>> Update(Guid id, UpdateCommentRequest request)
    {
        var comment = await _comments.UpdateAsync(id, User.GetUserId(), request.Body);
        return Ok(CommentDto.FromEntity(comment));
    }

    [HttpDelete("api/comments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _comments.DeleteAsync(id, User.GetUserId());
        return NoContent();
    }
}
