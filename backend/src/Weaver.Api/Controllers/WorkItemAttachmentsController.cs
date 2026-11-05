using Microsoft.AspNetCore.Mvc;
using Weaver.Api.Attachments;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Controllers;

[ApiController]
// The bearer challenge has no body.
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public class WorkItemAttachmentsController(IWorkItemAttachmentService attachments, MultipartFileReader uploads)
    : ControllerBase
{
    private readonly IWorkItemAttachmentService _attachments = attachments;
    private readonly MultipartFileReader _uploads = uploads;

    [HttpGet("api/work-items/{workItemId:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> ListForWorkItem(Guid workItemId)
    {
        var attachments = await _attachments.ListForWorkItemAsync(workItemId);
        return Ok(attachments.Select(AttachmentDto.FromView));
    }

    /// <summary>
    /// A <c>multipart/form-data</c> body with the file in a part named <c>file</c>. A file over
    /// the configured maximum is refused with 413. Like a URL link, an attachment's Location is
    /// the item's list.
    /// </summary>
    [HttpPost("api/work-items/{workItemId:guid}/attachments")]
    [ProducesResponseType<AttachmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    public async Task<ActionResult<AttachmentDto>> Upload(Guid workItemId)
    {
        var attachment = await _uploads.ReadFileAsync(
            Request,
            (fileName, contentType, content) =>
                _attachments.AddAsync(workItemId, fileName, contentType, content, HttpContext.RequestAborted),
            HttpContext.RequestAborted);
        return CreatedAtAction(nameof(ListForWorkItem), new { workItemId }, AttachmentDto.FromView(attachment));
    }

    /// <summary>
    /// Always sent as a download (<c>Content-Disposition: attachment</c>) and never sniffed, so
    /// an uploaded HTML or SVG file can't run as a page on the API's origin.
    /// </summary>
    [HttpGet("api/work-item-attachments/{id:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid id)
    {
        var (attachment, content) = await _attachments.OpenContentAsync(id, HttpContext.RequestAborted);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(content, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("api/work-item-attachments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _attachments.DeleteAsync(id);
        return NoContent();
    }
}
