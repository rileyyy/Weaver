namespace Weaver.Domain.Exceptions;

/// <summary>The attachment exists, but has no thumbnail: it isn't an image the server could decode, or predates thumbnails.</summary>
public class AttachmentThumbnailNotFoundException(Guid attachmentId)
    : DomainException(DomainErrorKind.NotFound, $"Attachment {attachmentId} has no thumbnail.")
{
}
