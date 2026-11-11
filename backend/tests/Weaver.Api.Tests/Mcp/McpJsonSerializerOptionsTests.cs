using System.Text.Json;
using Weaver.Api.Contracts;
using Weaver.Api.Mcp;
using Weaver.Application.Services;
using Weaver.Domain;

namespace Weaver.Api.Tests.Mcp;

/// <summary>The JSON every MCP tool result is written with.</summary>
[TestFixture]
public class McpJsonSerializerOptionsTests
{
    [Test]
    public void NullFields_AreLeftOut()
    {
        var summary = WorkItemSummaryDto.FromSummary(
            new WorkItemSummary(Guid.NewGuid(), 12, "Paint the fence", Guid.NewGuid(), "Home", null, "Doing", null, null, null, null, 0));

        var json = Serialize(summary);

        Assert.Multiple(() =>
        {
            Assert.That(json.TryGetProperty(nameof(WorkItemSummaryDto.Title), out _), Is.True);
            Assert.That(json.TryGetProperty(nameof(WorkItemSummaryDto.ChildCount), out _), Is.True);
            Assert.That(json.TryGetProperty(nameof(WorkItemSummaryDto.ParentId), out _), Is.False);
            Assert.That(json.TryGetProperty(nameof(WorkItemSummaryDto.TypeName), out _), Is.False);
            Assert.That(json.TryGetProperty(nameof(WorkItemSummaryDto.AssigneeName), out _), Is.False);
            Assert.That(json.TryGetProperty(nameof(WorkItemSummaryDto.TrashedAtUtc), out _), Is.False);
        });
    }

    [Test]
    public void Enums_AreWrittenByName()
    {
        var whoAmI = new WhoAmIDto("Claude Code", UserKind.Agent, "laptop", ApiKeyScope.ReadOnly);

        var json = Serialize(whoAmI);

        Assert.That(json.GetProperty(nameof(WhoAmIDto.AccountKind)).GetString(), Is.EqualTo("Agent"));
    }

    // A history entry exists to say what a value went from and to, so a cleared value is written
    // as null rather than left for the reader to infer from a missing property.
    [Test]
    public void AHistoryEntrysValues_StayEvenWhenNull()
    {
        var cleared = new WorkItemHistoryEntryDto(
            1, Guid.NewGuid(), DateTimeOffset.UnixEpoch, null, null, null, null, null, null, null,
            "description", JsonDocument.Parse("\"Old text\"").RootElement, NewValue: null, null, null, null);

        var json = Serialize(cleared);

        Assert.Multiple(() =>
        {
            Assert.That(json.GetProperty(nameof(WorkItemHistoryEntryDto.OldValue)).GetString(), Is.EqualTo("Old text"));
            Assert.That(json.GetProperty(nameof(WorkItemHistoryEntryDto.NewValue)).ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(json.TryGetProperty(nameof(WorkItemHistoryEntryDto.AccountName), out _), Is.False);
        });
    }

    private static JsonElement Serialize<T>(T value) =>
        JsonSerializer.SerializeToElement(value, McpJsonSerializerOptions.Default);
}
