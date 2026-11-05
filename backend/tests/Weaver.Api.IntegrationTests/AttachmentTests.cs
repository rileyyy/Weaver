using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Weaver.Api.Contracts;
using Weaver.Application.Attachments;
using Weaver.Domain;
using Weaver.Infrastructure;

namespace Weaver.Api.IntegrationTests;

/// <summary>Attachments over REST and MCP, against Postgres and the host's own attachments folder.</summary>
[TestFixture]
public class AttachmentTests
{
    private const int MaxBytes = WeaverApiFactory.MaxAttachmentMegabytes * 1024 * 1024;

    private HttpClient _client = null!;

    private static string AttachmentsPath => IntegrationTestEnvironment.Factory.AttachmentsPath;

    [SetUp]
    public async Task SetUp()
    {
        (_client, _) = await ApiClient.SignedInAsync();
    }

    [Test]
    public async Task AnUploadedFile_IsListed_AndDownloadsAsTheSameBytesWithItsNameAndType()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");
        var bytes = Enumerable.Range(0, 256).Select(b => (byte)b).ToArray();

        var uploaded = await UploadAsync(item.Id, "report.pdf", "application/pdf", bytes);
        var attachment = await uploaded.ReadAsync<AttachmentDto>();
        var listed = await (await _client.GetAsync($"/api/work-items/{item.Id}/attachments")).ReadAsync<List<AttachmentDto>>();
        var download = await _client.GetAsync($"/api/work-item-attachments/{attachment.Id}/content");

        Assert.Multiple(async () =>
        {
            Assert.That(uploaded.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(uploaded.Headers.Location?.ToString(), Does.EndWith($"/api/work-items/{item.Id}/attachments"));
            Assert.That(attachment, Is.EqualTo(new AttachmentDto(
                attachment.Id, item.Id, "report.pdf", "application/pdf", bytes.Length, attachment.CreatedAtUtc)));
            Assert.That(listed, Is.EqualTo(new[] { attachment }));
            Assert.That(download.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await download.Content.ReadAsByteArrayAsync(), Is.EqualTo(bytes));
            Assert.That(download.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/pdf"));
            Assert.That(download.Content.Headers.ContentDisposition?.DispositionType, Is.EqualTo("attachment"));
            Assert.That(download.Content.Headers.ContentDisposition?.FileName, Is.EqualTo("report.pdf"));
            Assert.That(download.Headers.GetValues("X-Content-Type-Options"), Is.EqualTo(new[] { "nosniff" }));
            Assert.That(File.Exists(Path.Combine(AttachmentsPath, attachment.Id.ToString())), Is.True);
        });
    }

    [Test]
    public async Task AnAttachment_IsSentInTheAgreedJsonShape()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");

        var json = await (await UploadAsync(item.Id, "a.txt", "text/plain", "hi"u8.ToArray())).Content.ReadAsStringAsync();

        var properties = JsonSerializer.Deserialize<JsonElement>(json).EnumerateObject().Select(p => p.Name);
        Assert.That(properties, Is.EquivalentTo(new[] { "id", "workItemId", "fileName", "contentType", "sizeBytes", "createdAtUtc" }));
    }

    [Test]
    public async Task AFileNamedWithAPath_IsStoredUnderItsIdAndNamedByItsLastSegment()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");

        var attachment = await (await UploadAsync(item.Id, "../../etc/passwd", "text/plain", "x"u8.ToArray()))
            .ReadAsync<AttachmentDto>();

        Assert.Multiple(() =>
        {
            Assert.That(attachment.FileName, Is.EqualTo("passwd"));
            Assert.That(File.Exists(Path.Combine(AttachmentsPath, attachment.Id.ToString())), Is.True);
        });
    }

    [Test]
    public async Task AFileAtTheLimit_IsAccepted_AndOneByteOver_Is413ProblemJsonNamingTheLimit_AndLeavesNothing()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");

        var atLimit = await UploadAsync(item.Id, "max.bin", "application/octet-stream", new byte[MaxBytes]);
        var filesBefore = Directory.GetFiles(AttachmentsPath).Length;
        var overLimit = await UploadAsync(item.Id, "over.bin", "application/octet-stream", new byte[MaxBytes + 1]);
        var listed = await (await _client.GetAsync($"/api/work-items/{item.Id}/attachments")).ReadAsync<List<AttachmentDto>>();

        Assert.Multiple(async () =>
        {
            Assert.That(atLimit.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(overLimit.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
            Assert.That(overLimit.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
            Assert.That(await overLimit.Content.ReadAsStringAsync(), Does.Contain($"{WeaverApiFactory.MaxAttachmentMegabytes} MB"));
            Assert.That(listed.Select(a => a.FileName), Is.EqualTo(new[] { "max.bin" }));
            Assert.That(Directory.GetFiles(AttachmentsPath), Has.Length.EqualTo(filesBefore));
        });
    }

    [Test]
    public async Task AnEmptyFile_OrNoFilePart_Is400_AndAnUnknownItem_Is404()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");

        var empty = await UploadAsync(item.Id, "empty.txt", "text/plain", []);
        var noFilePart = await _client.PostAsync(
            $"/api/work-items/{item.Id}/attachments",
            new MultipartFormDataContent { { new StringContent("hello"), "note" } });
        var unknown = await UploadAsync(Guid.NewGuid(), "a.txt", "text/plain", "x"u8.ToArray());

        Assert.Multiple(() =>
        {
            Assert.That(empty.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(noFilePart.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task Deleting_RemovesTheRowAndTheFile()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");
        var attachment = await (await UploadAsync(item.Id, "a.txt", "text/plain", "x"u8.ToArray())).ReadAsync<AttachmentDto>();

        var deleted = await _client.DeleteAsync($"/api/work-item-attachments/{attachment.Id}");
        var deletedAgain = await _client.DeleteAsync($"/api/work-item-attachments/{attachment.Id}");
        var download = await _client.GetAsync($"/api/work-item-attachments/{attachment.Id}/content");

        Assert.Multiple(() =>
        {
            Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(deletedAgain.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(download.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(File.Exists(Path.Combine(AttachmentsPath, attachment.Id.ToString())), Is.False);
        });
    }

    [Test]
    public async Task AddingAndRemoving_IsRecordedInHistory_NamedByTheFileName()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");
        var attachment = await (await UploadAsync(item.Id, "report.pdf", "application/pdf", "x"u8.ToArray())).ReadAsync<AttachmentDto>();
        await _client.DeleteAsync($"/api/work-item-attachments/{attachment.Id}");

        var history = await (await _client.GetAsync($"/api/work-items/{item.Id}/history"))
            .ReadAsync<List<WorkItemHistoryEntryDto>>();

        var entries = history.Where(e => e.Field == WorkItemHistoryField.Attachment).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(entries.Select(e => (e.OldName, e.NewName)), Is.EqualTo(new[]
            {
                ("report.pdf", (string?)null),
                ((string?)null, "report.pdf"),
            }));
            Assert.That(entries[1].NewValue?.GetProperty("id").GetGuid(), Is.EqualTo(attachment.Id));
        });
    }

    [Test]
    public async Task ATrashedItemsAttachments_AreKeptWithTheirFiles_AndBackWhenItIsRestored()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");
        var attachment = await (await UploadAsync(item.Id, "a.txt", "text/plain", "x"u8.ToArray())).ReadAsync<AttachmentDto>();

        (await _client.DeleteAsync($"/api/work-items/{item.Id}")).EnsureSuccessStatusCode();
        var whileTrashed = await _client.GetAsync($"/api/work-item-attachments/{attachment.Id}/content");
        (await _client.PostAsync($"/api/trash/{item.Id}/restore", null)).EnsureSuccessStatusCode();
        var restored = await _client.GetAsync($"/api/work-item-attachments/{attachment.Id}/content");

        Assert.Multiple(() =>
        {
            Assert.That(whileTrashed.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(restored.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(File.Exists(Path.Combine(AttachmentsPath, attachment.Id.ToString())), Is.True);
        });
    }

    // Purge (ticket 20) isn't built; this does what it will: clean up the files through the
    // seam, then delete the trashed item, whose attachment rows go by FK cascade.
    [Test]
    public async Task PurgingATrashedItem_ThroughTheCleanupSeam_RemovesItsFileAndRow()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");
        var attachment = await (await UploadAsync(item.Id, "a.txt", "text/plain", "x"u8.ToArray())).ReadAsync<AttachmentDto>();
        (await _client.DeleteAsync($"/api/work-items/{item.Id}")).EnsureSuccessStatusCode();

        using (var scope = IntegrationTestEnvironment.Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAttachmentFileCleanup>().DeleteFilesForWorkItemsAsync([item.Id]);
            await scope.ServiceProvider.GetRequiredService<WeaverDbContext>().WorkItems
                .IgnoreQueryFilters()
                .Where(w => w.Id == item.Id)
                .ExecuteDeleteAsync();
        }

        using var check = IntegrationTestEnvironment.Factory.Services.CreateScope();
        var rowsLeft = await check.ServiceProvider.GetRequiredService<WeaverDbContext>().WorkItemAttachments
            .IgnoreQueryFilters()
            .CountAsync(a => a.Id == attachment.Id);
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(AttachmentsPath, attachment.Id.ToString())), Is.False);
            Assert.That(rowsLeft, Is.Zero);
        });
    }

    [Test]
    public async Task AReadOnlyKey_CantUpload()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);

        var response = await UploadAsync(agent, item.Id, "a.txt", "text/plain", "x"u8.ToArray());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Mcp_WithAReadOnlyKey_ListsAndFetchesAttachments()
    {
        var item = await _client.CreateWorkItemAsync("Kitchen");
        var text = await (await UploadAsync(item.Id, "notes.md", "text/markdown", "# Notes"u8.ToArray())).ReadAsync<AttachmentDto>();
        var image = await (await UploadAsync(item.Id, "dot.png", "image/png", [0x89, 0x50, 0x4E, 0x47])).ReadAsync<AttachmentDto>();
        var (agent, _) = await ApiClient.SignedInAgentAsync(ApiKeyScope.ReadOnly);
        await using var mcp = await ConnectAsync(agent);

        var listed = await mcp.CallToolAsync("list_work_item_attachments", new Dictionary<string, object?> { ["workItemId"] = item.Id });
        var gotText = await mcp.CallToolAsync("get_work_item_attachment", new Dictionary<string, object?> { ["id"] = text.Id });
        var gotImage = await mcp.CallToolAsync("get_work_item_attachment", new Dictionary<string, object?> { ["id"] = image.Id });

        Assert.Multiple(() =>
        {
            Assert.That(listed.IsError, Is.Not.True, () => TextOf(listed));
            Assert.That(TextOf(listed), Does.Contain("notes.md").And.Contain("dot.png"));
            Assert.That(gotText.IsError, Is.Not.True, () => TextOf(gotText));
            Assert.That(ResourceOf(gotText), Is.TypeOf<TextResourceContents>());
            Assert.That((ResourceOf(gotText) as TextResourceContents)?.Text, Is.EqualTo("# Notes"));
            Assert.That(gotImage.IsError, Is.Not.True, () => TextOf(gotImage));
            Assert.That((ResourceOf(gotImage) as BlobResourceContents)?.DecodedData.ToArray(), Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        });
    }

    private Task<HttpResponseMessage> UploadAsync(Guid workItemId, string fileName, string contentType, byte[] bytes) =>
        UploadAsync(_client, workItemId, fileName, contentType, bytes);

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid workItemId,
        string fileName,
        string contentType,
        byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return client.PostAsync($"/api/work-items/{workItemId}/attachments", form);
    }

    private static ResourceContents? ResourceOf(CallToolResult result) =>
        result.Content.OfType<EmbeddedResourceBlock>().SingleOrDefault()?.Resource;

    private static string TextOf(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static Task<McpClient> ConnectAsync(HttpClient client) =>
        McpClient.CreateAsync(new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(client.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            client,
            loggerFactory: null,
            ownsHttpClient: false));
}
