namespace Weaver.Api.Mcp;

/// <summary>The optional argument every tool that writes to an existing work item takes.</summary>
internal static class ExpectedVersionArgument
{
    public const string Description =
        "The work item's version from your last read. If given and the item has changed since, the " +
        "write is rejected instead of overwriting someone else's change.";
}
