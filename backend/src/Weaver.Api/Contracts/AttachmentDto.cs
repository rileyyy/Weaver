using Weaver.Application.Services;

namespace Weaver.Api.Contracts;

public record AttachmentDto(
    Guid Id,
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc)
{
    public static AttachmentDto FromView(WorkItemAttachmentView attachment) => new(
        attachment.Id,
        attachment.WorkItemId,
        attachment.FileName,
        attachment.ContentType,
        attachment.SizeBytes,
        attachment.CreatedAtUtc);
}
