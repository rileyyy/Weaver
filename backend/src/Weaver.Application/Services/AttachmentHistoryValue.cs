using Weaver.Domain;

namespace Weaver.Application.Services;

/// <summary>What a <see cref="WorkItemHistoryField.Attachment"/> entry holds.</summary>
internal sealed record AttachmentHistoryValue(Guid Id, string FileName)
{
    public static AttachmentHistoryValue Of(WorkItemAttachment attachment) => new(attachment.Id, attachment.FileName);
}
