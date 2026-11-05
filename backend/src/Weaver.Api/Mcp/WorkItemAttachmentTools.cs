using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Weaver.Api.Contracts;
using Weaver.Application.Services;

namespace Weaver.Api.Mcp;

/// <summary>Read-only: agents can see and read attachments, but uploading and deleting are for people.</summary>
[McpServerToolType]
public class WorkItemAttachmentTools(IWorkItemAttachmentService attachments)
{
    /// <summary>
    /// The most one call returns. Base64 makes a file a third larger again, and much past this
    /// is more than an agent's context can usefully take; bigger files are for REST.
    /// </summary>
    public const long MaxFetchBytes = 5 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly IWorkItemAttachmentService _attachments = attachments;

    [McpServerTool(Name = "list_work_item_attachments", ReadOnly = true)]
    [Description("Lists the files attached to a work item, oldest first, each with its file name, content " +
        "type and size in bytes. Use get_work_item_attachment to read one.")]
    public async Task<IReadOnlyList<AttachmentDto>> ListWorkItemAttachments(
        [Description("The work item's id.")] Guid workItemId,
        CancellationToken ct)
    {
        var attachments = await _attachments.ListForWorkItemAsync(workItemId, ct);
        return attachments.Select(AttachmentDto.FromView).ToList();
    }

    [McpServerTool(Name = "get_work_item_attachment", ReadOnly = true)]
    [Description("Fetches one attachment: its metadata, then its content as an embedded resource. Text " +
        "(plain text, Markdown, CSV, JSON, XML and the like) comes as text; anything else as base64. " +
        "Files over 5 MB are refused.")]
    public Task<CallToolResult> GetWorkItemAttachment(
        [Description("The attachment's id, from list_work_item_attachments.")] Guid id,
        CancellationToken ct) =>
        McpExceptionTranslation.TranslateAsync(async () =>
        {
            var (attachment, content) = await _attachments.OpenContentAsync(id, ct);
            await using (content)
            {
                if (attachment.SizeBytes > MaxFetchBytes)
                {
                    throw new McpException(
                        $"Attachment \"{attachment.FileName}\" is {Megabytes(attachment.SizeBytes)} MB, over the " +
                        $"{Megabytes(MaxFetchBytes)} MB get_work_item_attachment returns. A person can download it " +
                        "from the work item instead.");
                }

                var bytes = await ReadAllAsync(content, attachment.SizeBytes, ct);
                return new CallToolResult
                {
                    Content =
                    [
                        new TextContentBlock
                        {
                            Text = JsonSerializer.Serialize(AttachmentDto.FromView(attachment), McpJsonSerializerOptions.Default),
                        },
                        new EmbeddedResourceBlock { Resource = ResourceFor(attachment, bytes) },
                    ],
                };
            }
        });

    private static ResourceContents ResourceFor(WorkItemAttachmentView attachment, byte[] bytes)
    {
        var uri = $"weaver://work-item-attachments/{attachment.Id}";
        if (TextContentTypes.IsTextLike(attachment.ContentType) && TryDecodeUtf8(bytes) is { } text)
        {
            return new TextResourceContents { Uri = uri, MimeType = attachment.ContentType, Text = text };
        }

        return BlobResourceContents.FromBytes(bytes, uri, attachment.ContentType);
    }

    /// <summary>A file labelled as text can still hold other bytes; those go as a blob rather than mangled text.</summary>
    private static string? TryDecodeUtf8(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream content, long sizeBytes, CancellationToken ct)
    {
        using var buffer = new MemoryStream((int)sizeBytes);
        await content.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static string Megabytes(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.#");
}
