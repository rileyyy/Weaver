namespace Weaver.Application.Services;

/// <summary>An attachment's metadata and its file, open for reading. The caller disposes <see cref="Content"/>.</summary>
public sealed record AttachmentContent(WorkItemAttachmentView Attachment, Stream Content);
