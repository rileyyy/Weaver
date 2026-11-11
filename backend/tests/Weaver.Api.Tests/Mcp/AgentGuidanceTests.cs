using System.Reflection;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Weaver.Api.Mcp;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public partial class AgentGuidanceTests
{
    // Clients put the instructions into every conversation, so they must stay short (ticket 32).
    private const int MaxInstructionTokens = 500;

    private static readonly IReadOnlyList<string> ToolNames = typeof(AgentGuidance).Assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
        .SelectMany(t => t.GetMethods())
        .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
        .OfType<string>()
        .ToList();

    [Test]
    public void ToolNames_AreFoundByReflection()
    {
        Assert.That(ToolNames, Does.Contain("create_work_item").And.Contain("whoami"));
    }

    [Test]
    public void ServerInstructions_StayUnderAbout500Tokens()
    {
        Assert.That(EstimateTokens(AgentGuidance.ServerInstructions), Is.LessThanOrEqualTo(MaxInstructionTokens));
    }

    [TestCase("one two three", 3)]
    [TestCase("list_work_item_types", 7)]
    [TestCase("\"#123\":", 5)]
    public void EstimateTokens_CountsEveryWordPieceAndSymbol(string text, int expected)
    {
        Assert.That(EstimateTokens(text), Is.EqualTo(expected));
    }

    [Test]
    public void ServerInstructions_PointAtTheReferenceResource()
    {
        Assert.That(AgentGuidance.ServerInstructions, Does.Contain(ReferenceResource.Uri));
    }

    [TestCase("- Space:")]
    [TestCase("- Type:")]
    [TestCase("- When:")]
    [TestCase("list_unresolved_when_items")]
    [TestCase("are patches")]
    [TestCase("expectedVersion")]
    [TestCase("actorLabel")]
    [TestCase("Ask a human")]
    [TestCase("list_dependency_conflicts reports a conflict")]
    [TestCase("#123")]
    public void ServerInstructions_CoverTheEssentials(string topic)
    {
        Assert.That(AgentGuidance.ServerInstructions, Does.Contain(topic));
    }

    [Test]
    public void ServerInstructions_OnlyNameToolsThatExist()
    {
        Assert.That(ToolNamesIn(AgentGuidance.ServerInstructions).Except(ToolNames), Is.Empty);
    }

    [Test]
    public void Reference_OnlyNamesToolsThatExist()
    {
        Assert.That(ToolNamesIn(AgentGuidance.Reference).Except(ToolNames), Is.Empty);
    }

    // A new tool must be documented here too, or agents reading the reference won't know it exists.
    [Test]
    public void Reference_DescribesEveryTool()
    {
        var listed = ToolListEntryPattern().Matches(AgentGuidance.Reference).Select(m => m.Groups[1].Value);

        Assert.That(ToolNames.Except(listed), Is.Empty);
    }

    [TestCase("Work item")]
    [TestCase("Number")]
    [TestCase("Space")]
    [TestCase("Type")]
    [TestCase("Custom field")]
    [TestCase("Roll-up")]
    [TestCase("Status category")]
    [TestCase("Completion date")]
    [TestCase("Done-hiding period")]
    [TestCase("View")]
    [TestCase("Board")]
    [TestCase("Backlog")]
    [TestCase("When")]
    [TestCase("Tag")]
    [TestCase("Link")]
    [TestCase("Attachment")]
    [TestCase("Dependency conflict")]
    [TestCase("History")]
    [TestCase("Changes since T")]
    [TestCase("Summary")]
    [TestCase("Trash")]
    [TestCase("Actor label")]
    public void Reference_DefinesTheGlossaryTerm(string term)
    {
        Assert.That(AgentGuidance.Reference, Does.Contain($"**{term}"));
    }

    [Test]
    public void ReferenceResource_ReturnsTheReference()
    {
        Assert.That(ReferenceResource.Read(), Is.EqualTo(AgentGuidance.Reference));
    }

    // Deliberately pessimistic: real tokenizers merge common words and often whole snake_case
    // names, so counting every word piece and symbol as its own token stays above the real count.
    private static int EstimateTokens(string text) => TokenPiecePattern().Count(text);

    [GeneratedRegex(@"[A-Za-z]+|\d+|[^\sA-Za-z\d]")]
    private static partial Regex TokenPiecePattern();

    private static IReadOnlyList<string> ToolNamesIn(string text) =>
        ToolNamePattern().Matches(text).Select(m => m.Value).Distinct().ToList();

    [GeneratedRegex(@"\b[a-z]+(?:_[a-z]+)+\b|\bwhoami\b")]
    private static partial Regex ToolNamePattern();

    [GeneratedRegex(@"^- `([a-z_]+)`:", RegexOptions.Multiline)]
    private static partial Regex ToolListEntryPattern();
}
