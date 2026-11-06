using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Weaver.Api.Attachments;
using Weaver.Api.Contracts;
using Weaver.Api.Controllers;
using Weaver.Application.Attachments;
using Weaver.Application.Services;
using Weaver.Domain.Exceptions;

namespace Weaver.Api.Tests;

[TestFixture]
public class WorkItemAttachmentsControllerTests
{
    private const string Boundary = "----weaver-test-boundary";

    private static readonly DateTimeOffset Created = new(2026, 11, 5, 9, 30, 0, TimeSpan.Zero);

    private Mock<IWorkItemAttachmentService> _attachments = null!;
    private WorkItemAttachmentsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _attachments = new Mock<IWorkItemAttachmentService>(MockBehavior.Strict);
        _controller = new WorkItemAttachmentsController(
            _attachments.Object,
            new MultipartFileReader(Options.Create(new AttachmentOptions { MaxFileSizeMegabytes = 1 })))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    [Test]
    public async Task ListForWorkItem_DelegatesToServiceAndReturnsOk()
    {
        var workItemId = Guid.NewGuid();
        var view = ViewOn(workItemId);
        _attachments.Setup(a => a.ListForWorkItemAsync(workItemId, It.IsAny<CancellationToken>())).ReturnsAsync([view]);

        var result = await _controller.ListForWorkItem(workItemId);

        var body = ((result.Result as OkObjectResult)!.Value as IEnumerable<AttachmentDto>)!.Single();
        Assert.That(body, Is.EqualTo(new AttachmentDto(view.Id, workItemId, "report.pdf", "application/pdf", 3, Created)));
    }

    [Test]
    public async Task Upload_StreamsTheFilePartToTheService_AndReturnsCreated()
    {
        var workItemId = Guid.NewGuid();
        var view = ViewOn(workItemId);
        string? received = null;
        _attachments
            .Setup(a => a.AddAsync(workItemId, "report.pdf", "application/pdf", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, string? _, string? _, Stream content, CancellationToken _) =>
            {
                received = await new StreamReader(content).ReadToEndAsync();
                return view;
            });
        SendMultipart(
            Part("note", null, null, "ignored"),
            Part("file", "report.pdf", "application/pdf", "PDF"));

        var result = await _controller.Upload(workItemId);

        var created = result.Result as CreatedAtActionResult;
        Assert.Multiple(() =>
        {
            Assert.That(received, Is.EqualTo("PDF"));
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.ActionName, Is.EqualTo(nameof(WorkItemAttachmentsController.ListForWorkItem)));
            Assert.That(created.Value, Is.EqualTo(AttachmentDto.FromView(view)));
        });
    }

    [Test]
    public async Task Upload_PrefersTheEncodedFileName_SoNonAsciiNamesSurvive()
    {
        var workItemId = Guid.NewGuid();
        _attachments
            .Setup(a => a.AddAsync(workItemId, "Füße.txt", "text/plain", It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ViewOn(workItemId));
        SendMultipart(
            "Content-Disposition: form-data; name=\"file\"; filename=\"Fusse.txt\"; filename*=UTF-8''F%C3%BC%C3%9Fe.txt\r\n" +
            "Content-Type: text/plain\r\n\r\nhi");

        await _controller.Upload(workItemId);

        _attachments.VerifyAll();
    }

    [Test]
    public void Upload_WithoutAFilePart_IsAValidationError()
    {
        SendMultipart(Part("other", "a.txt", "text/plain", "hi"));

        Assert.ThrowsAsync<DomainValidationException>(() => _controller.Upload(Guid.NewGuid()));
    }

    [Test]
    public void Upload_ThatIsNotMultipart_IsAValidationError()
    {
        var request = _controller.HttpContext.Request;
        request.ContentType = "application/json";
        request.Body = new MemoryStream("{}"u8.ToArray());

        Assert.ThrowsAsync<DomainValidationException>(() => _controller.Upload(Guid.NewGuid()));
    }

    [Test]
    public async Task Upload_RaisesTheRequestBodyLimitToFitTheMaximumFileSize()
    {
        var workItemId = Guid.NewGuid();
        var bodySize = new FakeMaxRequestBodySizeFeature();
        _controller.HttpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(bodySize);
        _attachments
            .Setup(a => a.AddAsync(workItemId, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ViewOn(workItemId));
        SendMultipart(Part("file", "a.txt", "text/plain", "hi"));

        await _controller.Upload(workItemId);

        Assert.That(bodySize.MaxRequestBodySize, Is.GreaterThan(1024 * 1024));
    }

    [Test]
    public async Task Download_ReturnsTheBytesAsAnAttachmentWithTheStoredContentType()
    {
        var id = Guid.NewGuid();
        var content = new MemoryStream([1, 2, 3]);
        _attachments.Setup(a => a.OpenContentAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentContent(ViewOn(Guid.NewGuid()) with { Id = id }, content));

        var result = await _controller.Download(id) as FileStreamResult;

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.FileStream, Is.SameAs(content));
            Assert.That(result.ContentType, Is.EqualTo("application/pdf"));
            Assert.That(result.FileDownloadName, Is.EqualTo("report.pdf"));
            Assert.That(_controller.Response.Headers.XContentTypeOptions.ToString(), Is.EqualTo("nosniff"));
        });
    }

    [Test]
    public async Task Delete_DelegatesToServiceAndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        _attachments.Setup(a => a.DeleteAsync(id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _controller.Delete(id);

        Assert.That(result, Is.TypeOf<NoContentResult>());
        _attachments.VerifyAll();
    }

    private static WorkItemAttachmentView ViewOn(Guid workItemId) =>
        new(Guid.NewGuid(), workItemId, "report.pdf", "application/pdf", 3, Created);

    private static string Part(string name, string? fileName, string? contentType, string body) =>
        $"Content-Disposition: form-data; name=\"{name}\"" +
        (fileName is null ? string.Empty : $"; filename=\"{fileName}\"") + "\r\n" +
        (contentType is null ? string.Empty : $"Content-Type: {contentType}\r\n") +
        "\r\n" + body;

    private void SendMultipart(params string[] parts)
    {
        var body = string.Concat(parts.Select(p => $"--{Boundary}\r\n{p}\r\n")) + $"--{Boundary}--\r\n";
        var request = _controller.HttpContext.Request;
        request.ContentType = $"multipart/form-data; boundary={Boundary}";
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
    }
}
