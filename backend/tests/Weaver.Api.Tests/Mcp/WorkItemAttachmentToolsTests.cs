using System.Reflection;
using System.Text;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using Weaver.Api.Contracts;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public class WorkItemAttachmentToolsTests
{
    private Mock<IWorkItemAttachmentService> _attachments = null!;
    private WorkItemAttachmentTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _attachments = new Mock<IWorkItemAttachmentService>(MockBehavior.Strict);
        _tools = new WorkItemAttachmentTools(_attachments.Object);
    }

    [Test]
    public async Task ListWorkItemAttachments_DelegatesToServiceAndMapsResults()
    {
        var workItemId = Guid.NewGuid();
        var view = View("report.pdf", "application/pdf", 3);
        _attachments.Setup(a => a.ListForWorkItemAsync(workItemId, It.IsAny<CancellationToken>())).ReturnsAsync([view]);

        var result = await _tools.ListWorkItemAttachments(workItemId, CancellationToken.None);

        Assert.That(result.Single(), Is.EqualTo(AttachmentDto.FromView(view)));
    }

    [Test]
    public async Task GetWorkItemAttachment_WithText_ReturnsTheMetadataAndTheTextItself()
    {
        var view = View("notes.md", "text/markdown; charset=utf-8", 7);
        Opens(view, Encoding.UTF8.GetBytes("# Notes"));

        var result = await _tools.GetWorkItemAttachment(view.Id, CancellationToken.None);

        var resource = (result.Content[1] as EmbeddedResourceBlock)?.Resource as TextResourceContents;
        Assert.Multiple(() =>
        {
            Assert.That(MetadataOf(result).GetProperty("FileName").GetString(), Is.EqualTo("notes.md"));
            Assert.That(resource?.Text, Is.EqualTo("# Notes"));
            Assert.That(resource?.MimeType, Is.EqualTo("text/markdown; charset=utf-8"));
            Assert.That(resource?.Uri, Does.Contain(view.Id.ToString()));
        });
    }

    [TestCase("application/json")]
    [TestCase("application/ld+json")]
    [TestCase("image/svg+xml")]
    [TestCase("text/csv")]
    public async Task GetWorkItemAttachment_TreatsStructuredTextTypesAsText(string contentType)
    {
        var view = View("data", contentType, 2);
        Opens(view, "{}"u8.ToArray());

        var result = await _tools.GetWorkItemAttachment(view.Id, CancellationToken.None);

        Assert.That((result.Content[1] as EmbeddedResourceBlock)?.Resource, Is.TypeOf<TextResourceContents>());
    }

    [Test]
    public async Task GetWorkItemAttachment_WithBinary_ReturnsTheBytesAsABlob()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var view = View("photo.png", "image/png", bytes.Length);
        Opens(view, bytes);

        var result = await _tools.GetWorkItemAttachment(view.Id, CancellationToken.None);

        var resource = (result.Content[1] as EmbeddedResourceBlock)?.Resource as BlobResourceContents;
        Assert.Multiple(() =>
        {
            Assert.That(resource?.DecodedData.ToArray(), Is.EqualTo(bytes));
            Assert.That(resource?.MimeType, Is.EqualTo("image/png"));
        });
    }

    [Test]
    public async Task GetWorkItemAttachment_WithATextTypeThatIsntValidUtf8_ReturnsABlob()
    {
        var bytes = new byte[] { 0xFF, 0xFE, 0x00 };
        var view = View("odd.txt", "text/plain", bytes.Length);
        Opens(view, bytes);

        var result = await _tools.GetWorkItemAttachment(view.Id, CancellationToken.None);

        Assert.That((result.Content[1] as EmbeddedResourceBlock)?.Resource, Is.TypeOf<BlobResourceContents>());
    }

    [Test]
    public void GetWorkItemAttachment_OverTheFetchCap_IsRefusedWithAClearError()
    {
        var view = View("huge.zip", "application/zip", WorkItemAttachmentTools.MaxFetchBytes + 1);
        var content = new MemoryStream();
        _attachments.Setup(a => a.OpenContentAsync(view.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentContent(view, content));

        var thrown = Assert.ThrowsAsync<McpException>(() => _tools.GetWorkItemAttachment(view.Id, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Message, Does.Contain("huge.zip").And.Contain("MB"));
            Assert.That(content.CanRead, Is.False, "the file is closed again");
        });
    }

    [Test]
    public void GetWorkItemAttachment_WhenNotFound_ThrowsMcpExceptionWithSameMessage()
    {
        var id = Guid.NewGuid();
        var notFound = new EntityNotFoundException(nameof(WorkItemAttachment), id);
        _attachments.Setup(a => a.OpenContentAsync(id, It.IsAny<CancellationToken>())).ThrowsAsync(notFound);

        var thrown = Assert.ThrowsAsync<McpException>(() => _tools.GetWorkItemAttachment(id, CancellationToken.None));

        Assert.That(thrown!.Message, Is.EqualTo(notFound.Message));
    }

    [Test]
    public void BothToolsAreReadOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToolOf(nameof(WorkItemAttachmentTools.ListWorkItemAttachments)).ReadOnly, Is.True);
            Assert.That(ToolOf(nameof(WorkItemAttachmentTools.GetWorkItemAttachment)).ReadOnly, Is.True);
        });
    }

    private void Opens(WorkItemAttachmentView view, byte[] bytes) =>
        _attachments.Setup(a => a.OpenContentAsync(view.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentContent(view, new MemoryStream(bytes)));

    private static WorkItemAttachmentView View(string fileName, string contentType, long size) =>
        new(Guid.NewGuid(), Guid.NewGuid(), fileName, contentType, size, DateTimeOffset.UnixEpoch);

    private static JsonElement MetadataOf(CallToolResult result) =>
        JsonSerializer.Deserialize<JsonElement>(((TextContentBlock)result.Content[0]).Text);

    private static McpServerToolAttribute ToolOf(string method) =>
        typeof(WorkItemAttachmentTools).GetMethod(method)!.GetCustomAttribute<McpServerToolAttribute>()!;
}
