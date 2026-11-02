namespace Weaver.Api.Mcp;

/// <summary>The optional argument every tool that changes a work item takes.</summary>
internal static class ActorLabelArgument
{
    public const string Description =
        "Optional. Names the part of you making this change (e.g. \"sub-agent: test-writer\"), at most " +
        "100 characters. It's shown with the change in the work item's history.";
}
