using System.Reflection;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using Weaver.Api.Mcp;

namespace Weaver.Api.Tests.Mcp;

[TestFixture]
public partial class AgentGuidanceTests
{
    // Clients put the instructions into every conversation, so they must stay short (ticket 32).
    // Four characters per token is the usual estimate for English text.
    private const int MaxInstructionTokens = 500;
    private const int CharactersPerToken = 4;

    private static readonly IReadOnlyList<string> ToolNames = typeof(WorkItemTools).Assembly.GetTypes()
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
        var estimatedTokens = AgentGuidance.ServerInstructions.Length / CharactersPerToken;

        Assert.That(estimatedTokens, Is.LessThanOrEqualTo(MaxInstructionTokens));
    }

    [Test]
    public void ServerInstructions_PointAtTheReferenceResource()
    {
        Assert.That(AgentGuidance.ServerInstructions, Does.Contain(ReferenceResource.Uri));
    }

    [TestCase("Space")]
    [TestCase("Type")]
    [TestCase("When")]
    [TestCase("patches")]
    [TestCase("expectedVersion")]
    [TestCase("actorLabel")]
    [TestCase("Ask a human")]
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

    private static IReadOnlyList<string> ToolNamesIn(string text) =>
        ToolNamePattern().Matches(text).Select(m => m.Value).Distinct().ToList();

    [GeneratedRegex(@"\b[a-z]+(?:_[a-z]+)+\b|\bwhoami\b")]
    private static partial Regex ToolNamePattern();

    [GeneratedRegex(@"^- `([a-z_]+)`:", RegexOptions.Multiline)]
    private static partial Regex ToolListEntryPattern();
}
