namespace Weaver.Application.Services;

public record WorkItemAttachmentView(
    Guid Id,
    Guid WorkItemId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc,
    bool HasThumbnail);
