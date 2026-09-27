using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Auth;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
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
        return Ok(comments.Select(CommentDto.FromView));
    }

    [HttpGet("api/comments/{id:guid}")]
    public async Task<ActionResult<CommentDto>> GetById(Guid id)
    {
        var comment = await _comments.GetAsync(id);
        return comment is null ? NotFound() : Ok(CommentDto.FromView(comment));
    }

    [HttpPost("api/work-items/{workItemId:guid}/comments")]
    [ProducesResponseType<CommentDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CommentDto>> Create(Guid workItemId, CreateCommentRequest request)
    {
        var comment = await _comments.CreateAsync(workItemId, User.GetUserId(), request.Body);
        var dto = CommentDto.FromView(comment);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("api/comments/{id:guid}")]
    public async Task<ActionResult<CommentDto>> Update(Guid id, UpdateCommentRequest request)
    {
        var comment = await _comments.UpdateAsync(id, User.GetUserId(), request.Body);
        return Ok(CommentDto.FromView(comment));
    }

    [HttpDelete("api/comments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _comments.DeleteAsync(id, User.GetUserId());
        return NoContent();
    }
}
