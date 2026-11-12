using Weaver.Domain.Exceptions;

namespace Weaver.Domain;

/// <summary>
/// A file attached to a work item. Only its metadata lives here; the bytes are stored under
/// the attachments path, named by <see cref="Id"/> alone, so nothing the uploader chose ends
/// up in a path on disk.
/// </summary>
public class WorkItemAttachment : IHasCreatedAt
{
    public const int FileNameMaxLength = 255;
    public const int ContentTypeMaxLength = 255;
    public const string FallbackFileName = "attachment";
    public const string FallbackContentType = "application/octet-stream";

    /// <summary>For EF Core.</summary>
    private WorkItemAttachment()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid WorkItemId { get; private set; }

    /// <summary>Display text only, never a path.</summary>
    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    /// <summary>
    /// Whether a thumbnail is stored beside the file. Only images the server could decode get one,
    /// and attachments added before thumbnails existed never do.
    /// </summary>
    public bool HasThumbnail { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Null once the uploader's account is gone.</summary>
    public Guid? UploadedByUserId { get; private set; }

    public WorkItem? WorkItem { get; private set; }

    public User? UploadedByUser { get; private set; }

    /// <summary>
    /// The id is the caller's because the file is written under it before the row is saved.
    /// A name or content type that can't be used is replaced rather than refused: the file
    /// itself is fine, and both are only shown back.
    /// </summary>
    public static WorkItemAttachment Create(
        Guid id,
        Guid workItemId,
        string? fileName,
        string? contentType,
        long sizeBytes,
        Guid? uploadedByUserId,
        bool hasThumbnail = false)
    {
        if (sizeBytes <= 0)
        {
            throw new DomainValidationException("The file is empty.");
        }

        return new WorkItemAttachment
        {
            Id = id,
            WorkItemId = workItemId,
            FileName = AttachmentFileName.Sanitize(fileName, FileNameMaxLength) ?? FallbackFileName,
            ContentType = AttachmentContentType.Sanitize(contentType, ContentTypeMaxLength) ?? FallbackContentType,
            SizeBytes = sizeBytes,
            UploadedByUserId = uploadedByUserId,
            HasThumbnail = hasThumbnail,
        };
    }
}
